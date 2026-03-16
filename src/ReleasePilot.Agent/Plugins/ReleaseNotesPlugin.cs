using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using ReleasePilot.Domain.Ports;

namespace ReleasePilot.Agent.Plugins;

/// <summary>
/// Semantic Kernel plugin that provides tools for the AI release notes agent.
/// These are the four required tool definitions called within the agent's tool-calling loop.
/// </summary>
public class ReleaseNotesPlugin
{
    private readonly IIssueTrackerPort _issueTracker;
    private readonly ILogger<ReleaseNotesPlugin> _logger;
    private readonly Dictionary<string, List<string>> _clarifications = new();
    private readonly Dictionary<string, string> _breakingChangeFlags = new();
    private ReleaseNotesDraft? _submittedDraft;

    public ReleaseNotesDraft? SubmittedDraft => _submittedDraft;

    public ReleaseNotesPlugin(IIssueTrackerPort issueTracker, ILogger<ReleaseNotesPlugin> logger)
    {
        _issueTracker = issueTracker;
        _logger = logger;
    }

    [KernelFunction("GetWorkItems")]
    [Description("Retrieves work items associated with a promotion. Returns id, title, description, and status for each work item.")]
    public async Task<string> GetWorkItemsAsync(
        [Description("Comma-separated list of work item references")] string workItemReferences)
    {
        _logger.LogInformation("[AGENT TOOL] GetWorkItems called with: {References}", workItemReferences);

        var references = workItemReferences.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var items = await _issueTracker.GetWorkItemsAsync(references);

        var result = items.Select(i => $"- [{i.Id}] {i.Title}: {i.Description} (Status: {i.Status})");
        return string.Join("\n", result);
    }

    [KernelFunction("AskClarification")]
    [Description("Asks a clarification question about a work item when its description is too vague to produce good release notes.")]
    public string AskClarification(
        [Description("The work item ID to ask about")] string workItemId,
        [Description("The clarification question")] string question)
    {
        _logger.LogInformation("[AGENT TOOL] AskClarification for {WorkItemId}: {Question}", workItemId, question);

        if (!_clarifications.ContainsKey(workItemId))
            _clarifications[workItemId] = [];

        _clarifications[workItemId].Add(question);

        // In a real system, this would pause and wait for human input.
        // For the stub, return a simulated response.
        return $"Clarification noted for {workItemId}: '{question}'. " +
               "No additional context is available from the team at this time. " +
               "Please proceed with the available information and flag as needing review.";
    }

    [KernelFunction("FlagBreakingChange")]
    [Description("Flags a work item as containing a breaking change with a reason explaining why.")]
    public string FlagBreakingChange(
        [Description("The work item ID that contains a breaking change")] string workItemId,
        [Description("The reason this is considered a breaking change")] string reason)
    {
        _logger.LogInformation("[AGENT TOOL] FlagBreakingChange for {WorkItemId}: {Reason}", workItemId, reason);

        _breakingChangeFlags[workItemId] = reason;
        return $"Work item {workItemId} has been flagged as a breaking change. Reason: {reason}";
    }

    [KernelFunction("SubmitReleaseNotes")]
    [Description("Submits the final structured release notes draft. The draft should be a JSON string with summary, categories (features, fixes, breaking), and open questions.")]
    public string SubmitReleaseNotes(
        [Description("JSON string of the release notes draft with fields: summary, categories (array of {name, items[]}), openQuestions")] string draftJson)
    {
        _logger.LogInformation("[AGENT TOOL] SubmitReleaseNotes called");

        try
        {
            var draft = System.Text.Json.JsonSerializer.Deserialize<ReleaseNotesDraft>(draftJson,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (draft is not null)
            {
                _submittedDraft = draft;
                _logger.LogInformation("[AGENT TOOL] Release notes submitted successfully with {CategoryCount} categories",
                    draft.Categories.Count);
                return "Release notes draft submitted successfully.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AGENT TOOL] Failed to parse release notes JSON");
        }

        return "Failed to parse the release notes draft. Please ensure it's valid JSON with summary, categories, and openQuestions.";
    }
}
