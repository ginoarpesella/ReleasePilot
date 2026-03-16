namespace ReleasePilot.Application.ReadModels;

public record PromotionDetailReadModel(
    Guid Id,
    string ApplicationName,
    string Version,
    string SourceEnvironment,
    string TargetEnvironment,
    string Status,
    string RequestedBy,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string? RollbackReason,
    List<string> WorkItemReferences,
    List<StateTransitionReadModel> StateHistory);

public record StateTransitionReadModel(
    string FromStatus,
    string ToStatus,
    string ActingUser,
    DateTime TransitionedAt,
    string? Reason);
