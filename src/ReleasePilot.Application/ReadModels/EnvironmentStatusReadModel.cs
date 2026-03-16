namespace ReleasePilot.Application.ReadModels;

public record EnvironmentStatusReadModel(
    string ApplicationName,
    List<EnvironmentPromotionStatus> Environments);

public record EnvironmentPromotionStatus(
    string Environment,
    Guid? ActivePromotionId,
    string? CurrentVersion,
    string? Status,
    DateTime? LastUpdated);
