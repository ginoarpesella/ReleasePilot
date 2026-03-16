using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ReleasePilot.Agent.Plugins;
using ReleasePilot.Domain.Ports;

namespace ReleasePilot.Agent;

/// <summary>
/// AI Release Notes Agent that uses Semantic Kernel to implement a proper tool-calling loop.
/// When a Promotion reaches the Approved state, this agent:
/// 1. Retrieves work items via IIssueTrackerPort
/// 2. Reasons over work item descriptions for release note suitability
/// 3. Invokes AskClarification if descriptions are too vague
/// 4. Flags items suggesting a breaking change
/// 5. Produces a structured draft: summary, categorized changes, and open questions
/// </summary>
public class ReleaseNotesAgent
{
    private readonly IIssueTrackerPort _issueTracker;
    private readonly ILogger<ReleaseNotesAgent> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Kernel? _kernel;
    private readonly bool _useMockedBackend;

    public ReleaseNotesAgent(
        IIssueTrackerPort issueTracker,
        ILogger<ReleaseNotesAgent> logger,
        ILoggerFactory loggerFactory,
        Kernel? kernel = null)
    {
        _issueTracker = issueTracker;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _kernel = kernel;
        _useMockedBackend = kernel is null;
    }

    public async Task<ReleaseNotesDraft> GenerateReleaseNotesAsync(
        Guid promotionId,
        string applicationName,
        string version,
        List<string> workItemReferences,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Release Notes Agent triggered for promotion {PromotionId} ({App} v{Version})",
            promotionId, applicationName, version);

        if (_useMockedBackend)
        {
            return await GenerateWithMockedBackendAsync(workItemReferences, applicationName, version, cancellationToken);
        }

        return await GenerateWithSemanticKernelAsync(workItemReferences, applicationName, version, cancellationToken);
    }

    /// <summary>
    /// Uses Semantic Kernel with a real LLM to perform the tool-calling loop.
    /// The agent iterates: call LLM → LLM requests tool calls → execute tools → return results → repeat.
    /// </summary>
    private async Task<ReleaseNotesDraft> GenerateWithSemanticKernelAsync(
        List<string> workItemReferences,
        string applicationName,
        string version,
        CancellationToken cancellationToken)
    {
        var plugin = new ReleaseNotesPlugin(_issueTracker, _loggerFactory.CreateLogger<ReleaseNotesPlugin>());
        _kernel!.Plugins.AddFromObject(plugin, "ReleaseNotes");

        var chatService = _kernel.GetRequiredService<IChatCompletionService>();

        var settings = new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            MaxTokens = 4096
        };

        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(GetSystemPrompt());
        chatHistory.AddUserMessage(GetUserPrompt(applicationName, version, workItemReferences));

        _logger.LogInformation("Starting Semantic Kernel tool-calling loop...");

        var response = await chatService.GetChatMessageContentAsync(
            chatHistory, settings, _kernel, cancellationToken);

        _logger.LogInformation("Agent completed. Response length: {Length}", response.Content?.Length ?? 0);

        // The agent should have called SubmitReleaseNotes as a tool
        if (plugin.SubmittedDraft is not null)
        {
            return plugin.SubmittedDraft;
        }

        // Fallback: try to parse the response as release notes
        _logger.LogWarning("Agent did not call SubmitReleaseNotes. Attempting to parse response.");
        return CreateFallbackDraft(applicationName, version);
    }

    /// <summary>
    /// Mocked LLM backend that simulates the agent's tool-calling loop behavior.
    /// Demonstrates proper agent loop: reason → call tool → reason → call tool → submit.
    /// </summary>
    private async Task<ReleaseNotesDraft> GenerateWithMockedBackendAsync(
        List<string> workItemReferences,
        string applicationName,
        string version,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("[MOCKED AGENT] Starting simulated tool-calling loop...");

        var plugin = new ReleaseNotesPlugin(_issueTracker, _loggerFactory.CreateLogger<ReleaseNotesPlugin>());

        // Step 1: Agent decides to retrieve work items (tool call)
        _logger.LogInformation("[MOCKED AGENT] Step 1: Calling GetWorkItems tool...");
        var workItemsResult = await plugin.GetWorkItemsAsync(string.Join(",", workItemReferences));
        _logger.LogInformation("[MOCKED AGENT] Retrieved work items:\n{WorkItems}", workItemsResult);

        // Step 2: Agent reasons over work items and identifies vague descriptions
        _logger.LogInformation("[MOCKED AGENT] Step 2: Reasoning over work items for clarity...");
        foreach (var reference in workItemReferences)
        {
            var items = await _issueTracker.GetWorkItemsAsync([reference], cancellationToken);
            var item = items.FirstOrDefault();
            if (item is not null && item.Description.Length < 40)
            {
                _logger.LogInformation("[MOCKED AGENT] Description of {Id} is vague. Calling AskClarification...", item.Id);
                plugin.AskClarification(item.Id, $"The description '{item.Description}' is too brief. Can you provide more details about what changes were made?");
            }
        }

        // Step 3: Agent identifies breaking changes (tool call)
        _logger.LogInformation("[MOCKED AGENT] Step 3: Scanning for breaking changes...");
        var allItems = await _issueTracker.GetWorkItemsAsync(workItemReferences, cancellationToken);
        foreach (var item in allItems)
        {
            if (item.Description.Contains("BREAKING", StringComparison.OrdinalIgnoreCase) ||
                item.Title.Contains("breaking", StringComparison.OrdinalIgnoreCase) ||
                item.Description.Contains("migration", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("[MOCKED AGENT] Flagging {Id} as breaking change...", item.Id);
                plugin.FlagBreakingChange(item.Id,
                    $"Work item mentions breaking changes or requires migration: {item.Title}");
            }
        }

        // Step 4: Agent produces structured draft and submits (tool call)
        _logger.LogInformation("[MOCKED AGENT] Step 4: Producing structured release notes draft...");

        var features = new List<ReleaseNoteItem>();
        var fixes = new List<ReleaseNoteItem>();
        var breaking = new List<ReleaseNoteItem>();
        var openQuestions = new List<string>();

        foreach (var item in allItems)
        {
            var isBreaking = item.Description.Contains("BREAKING", StringComparison.OrdinalIgnoreCase) ||
                             item.Description.Contains("migration", StringComparison.OrdinalIgnoreCase);

            var noteItem = new ReleaseNoteItem(
                item.Id, item.Title, item.Description,
                isBreaking,
                isBreaking ? $"Contains breaking changes: {item.Title}" : null);

            if (isBreaking)
                breaking.Add(noteItem);
            else if (item.Title.Contains("fix", StringComparison.OrdinalIgnoreCase) ||
                     item.Title.Contains("bug", StringComparison.OrdinalIgnoreCase))
                fixes.Add(noteItem);
            else
                features.Add(noteItem);

            if (item.Description.Length < 40)
                openQuestions.Add($"Work item {item.Id} needs more detail: '{item.Title}'");
        }

        var draft = new ReleaseNotesDraft(
            $"Release {version} of {applicationName} includes {allItems.Count} changes: " +
            $"{features.Count} features, {fixes.Count} fixes, and {breaking.Count} breaking changes.",
            [
                new ReleaseNoteCategory("Features", features),
                new ReleaseNoteCategory("Bug Fixes", fixes),
                new ReleaseNoteCategory("Breaking Changes", breaking)
            ],
            openQuestions);

        var draftJson = JsonSerializer.Serialize(draft, new JsonSerializerOptions { WriteIndented = true });
        plugin.SubmitReleaseNotes(draftJson);

        _logger.LogInformation("[MOCKED AGENT] Tool-calling loop complete. Draft submitted.");
        return draft;
    }

    private static ReleaseNotesDraft CreateFallbackDraft(string applicationName, string version)
    {
        return new ReleaseNotesDraft(
            $"Release notes for {applicationName} v{version}",
            [new ReleaseNoteCategory("Changes", [])],
            ["Agent did not produce structured output. Manual review required."]);
    }

    private static string GetSystemPrompt() => """
        You are a release notes drafting agent. Your job is to produce structured, clear release notes
        for software promotions. You have access to the following tools:
        
        1. GetWorkItems - Retrieves work item details for a promotion
        2. AskClarification - Asks for clarification when a work item description is too vague
        3. FlagBreakingChange - Flags work items that contain breaking changes
        4. SubmitReleaseNotes - Submits the final structured release notes draft
        
        Your workflow:
        1. First, call GetWorkItems to retrieve all work items
        2. Review each work item - if the description is too vague (less than 20 words), call AskClarification
        3. For any items mentioning "breaking", "migration", "schema change", or similar, call FlagBreakingChange
        4. Categorize items into: Features, Bug Fixes, Breaking Changes
        5. Finally, call SubmitReleaseNotes with a JSON draft containing summary, categories, and openQuestions
        
        Always call SubmitReleaseNotes at the end with the complete draft.
        """;

    private static string GetUserPrompt(string applicationName, string version, List<string> workItemReferences) =>
        $"Generate release notes for {applicationName} v{version}. " +
        $"Work item references: {string.Join(", ", workItemReferences)}. " +
        "Follow the workflow: get work items, ask clarifications for vague descriptions, " +
        "flag breaking changes, then submit the structured release notes.";
}
