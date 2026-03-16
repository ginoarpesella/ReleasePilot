using ReleasePilot.Domain.Enums;

namespace ReleasePilot.Domain.Events;

public record PromotionRequested(
    Guid EventId,
    DateTime OccurredAt,
    Guid PromotionId,
    string ApplicationName,
    string Version,
    DeploymentEnvironment SourceEnvironment,
    DeploymentEnvironment TargetEnvironment,
    string RequestedBy,
    List<string> WorkItemReferences) : IDomainEvent
{
    public string EventType => nameof(PromotionRequested);
}

public record PromotionApproved(
    Guid EventId,
    DateTime OccurredAt,
    Guid PromotionId,
    string ApprovedBy) : IDomainEvent
{
    public string EventType => nameof(PromotionApproved);
}

public record DeploymentStarted(
    Guid EventId,
    DateTime OccurredAt,
    Guid PromotionId,
    string StartedBy) : IDomainEvent
{
    public string EventType => nameof(DeploymentStarted);
}

public record PromotionCompleted(
    Guid EventId,
    DateTime OccurredAt,
    Guid PromotionId,
    DateTime CompletionTimestamp) : IDomainEvent
{
    public string EventType => nameof(PromotionCompleted);
}

public record PromotionRolledBack(
    Guid EventId,
    DateTime OccurredAt,
    Guid PromotionId,
    string Reason,
    string RolledBackBy) : IDomainEvent
{
    public string EventType => nameof(PromotionRolledBack);
}

public record PromotionCancelled(
    Guid EventId,
    DateTime OccurredAt,
    Guid PromotionId,
    string CancelledBy) : IDomainEvent
{
    public string EventType => nameof(PromotionCancelled);
}
