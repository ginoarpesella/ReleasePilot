namespace ReleasePilot.Application.ReadModels;

public record PromotionSummaryReadModel(
    Guid Id,
    string Version,
    string SourceEnvironment,
    string TargetEnvironment,
    string Status,
    string RequestedBy,
    DateTime CreatedAt);
