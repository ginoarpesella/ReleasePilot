namespace ReleasePilot.Agent;

/// <summary>
/// Represents a structured release notes draft produced by the AI agent.
/// </summary>
public record ReleaseNotesDraft(
    string Summary,
    List<ReleaseNoteCategory> Categories,
    List<string> OpenQuestions);

public record ReleaseNoteCategory(
    string Name,
    List<ReleaseNoteItem> Items);

public record ReleaseNoteItem(
    string WorkItemId,
    string Title,
    string Description,
    bool IsBreakingChange,
    string? BreakingChangeReason);
