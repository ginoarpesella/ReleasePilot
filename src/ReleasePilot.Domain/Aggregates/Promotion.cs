using ReleasePilot.Domain.Enums;
using ReleasePilot.Domain.Events;
using ReleasePilot.Domain.Exceptions;
using ReleasePilot.Domain.ValueObjects;

namespace ReleasePilot.Domain.Aggregates;

public class Promotion
{
    public Guid Id { get; private set; }
    public string ApplicationName { get; private set; } = default!;
    public string Version { get; private set; } = default!;
    public DeploymentEnvironment SourceEnvironment { get; private set; }
    public DeploymentEnvironment TargetEnvironment { get; private set; }
    public PromotionStatus Status { get; private set; }
    public string RequestedBy { get; private set; } = default!;
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? RollbackReason { get; private set; }
    public List<string> WorkItemReferences { get; private set; } = [];
    public uint RowVersion { get; private set; }

    private readonly List<StateTransition> _stateHistory = [];
    public IReadOnlyList<StateTransition> StateHistory => _stateHistory.AsReadOnly();

    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private Promotion() { } // EF Core constructor

    public static Promotion Request(
        string applicationName,
        string version,
        DeploymentEnvironment sourceEnvironment,
        DeploymentEnvironment targetEnvironment,
        string requestedBy,
        List<string>? workItemReferences = null)
    {
        if (string.IsNullOrWhiteSpace(applicationName))
            throw new DomainException("INVALID_APPLICATION", "Application name is required.");

        if (string.IsNullOrWhiteSpace(version))
            throw new DomainException("INVALID_VERSION", "Version is required.");

        if (string.IsNullOrWhiteSpace(requestedBy))
            throw new DomainException("INVALID_USER", "Requesting user is required.");

        if (!DeploymentEnvironmentExtensions.IsValidPromotion(sourceEnvironment, targetEnvironment))
            throw new DomainException("INVALID_PROMOTION_PATH",
                $"Cannot promote from {sourceEnvironment} to {targetEnvironment}. " +
                $"Environments must follow the order: Dev → Staging → Production.");

        var promotion = new Promotion
        {
            Id = Guid.NewGuid(),
            ApplicationName = applicationName,
            Version = version,
            SourceEnvironment = sourceEnvironment,
            TargetEnvironment = targetEnvironment,
            Status = PromotionStatus.Requested,
            RequestedBy = requestedBy,
            CreatedAt = DateTime.UtcNow,
            WorkItemReferences = workItemReferences ?? []
        };

        promotion._stateHistory.Add(new StateTransition(
            PromotionStatus.Requested, PromotionStatus.Requested, requestedBy, promotion.CreatedAt));

        promotion.RaiseDomainEvent(new PromotionRequested(
            Guid.NewGuid(), DateTime.UtcNow,
            promotion.Id, applicationName, version,
            sourceEnvironment, targetEnvironment,
            requestedBy, promotion.WorkItemReferences));

        return promotion;
    }

    public void Approve(string approvedBy, bool isApprover, bool hasInProgressPromotionForEnvironment)
    {
        EnsureMutable();

        if (Status != PromotionStatus.Requested)
            throw new DomainException("INVALID_TRANSITION",
                $"Cannot approve a promotion in '{Status}' state. Must be in 'Requested' state.");

        if (!isApprover)
            throw new DomainException("UNAUTHORIZED_APPROVER",
                "Only users with the Approver role can approve promotions.");

        if (hasInProgressPromotionForEnvironment)
            throw new DomainException("ENVIRONMENT_LOCKED",
                $"Another promotion is already in progress for environment '{TargetEnvironment}'. " +
                "Wait for it to complete or roll back before approving a new one.");

        var previousStatus = Status;
        Status = PromotionStatus.Approved;

        _stateHistory.Add(new StateTransition(previousStatus, Status, approvedBy, DateTime.UtcNow));
        RaiseDomainEvent(new PromotionApproved(Guid.NewGuid(), DateTime.UtcNow, Id, approvedBy));
    }

    public void StartDeployment(string startedBy)
    {
        EnsureMutable();

        if (Status != PromotionStatus.Approved)
            throw new DomainException("INVALID_TRANSITION",
                $"Cannot start deployment for a promotion in '{Status}' state. Must be in 'Approved' state.");

        var previousStatus = Status;
        Status = PromotionStatus.InProgress;

        _stateHistory.Add(new StateTransition(previousStatus, Status, startedBy, DateTime.UtcNow));
        RaiseDomainEvent(new DeploymentStarted(Guid.NewGuid(), DateTime.UtcNow, Id, startedBy));
    }

    public void Complete()
    {
        EnsureMutable();

        if (Status != PromotionStatus.InProgress)
            throw new DomainException("INVALID_TRANSITION",
                $"Cannot complete a promotion in '{Status}' state. Must be in 'InProgress' state.");

        var previousStatus = Status;
        Status = PromotionStatus.Completed;
        CompletedAt = DateTime.UtcNow;

        _stateHistory.Add(new StateTransition(previousStatus, Status, "system", CompletedAt.Value));
        RaiseDomainEvent(new PromotionCompleted(Guid.NewGuid(), DateTime.UtcNow, Id, CompletedAt.Value));
    }

    public void Rollback(string reason, string rolledBackBy)
    {
        EnsureMutable();

        if (Status != PromotionStatus.InProgress)
            throw new DomainException("INVALID_TRANSITION",
                $"Cannot roll back a promotion in '{Status}' state. Must be in 'InProgress' state.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("REASON_REQUIRED", "A reason must be provided when rolling back a promotion.");

        var previousStatus = Status;
        Status = PromotionStatus.RolledBack;
        RollbackReason = reason;

        _stateHistory.Add(new StateTransition(previousStatus, Status, rolledBackBy, DateTime.UtcNow, reason));
        RaiseDomainEvent(new PromotionRolledBack(Guid.NewGuid(), DateTime.UtcNow, Id, reason, rolledBackBy));
    }

    public void Cancel(string cancelledBy)
    {
        EnsureMutable();

        if (Status != PromotionStatus.Requested)
            throw new DomainException("INVALID_TRANSITION",
                $"Cannot cancel a promotion in '{Status}' state. Must be in 'Requested' state.");

        var previousStatus = Status;
        Status = PromotionStatus.Cancelled;

        _stateHistory.Add(new StateTransition(previousStatus, Status, cancelledBy, DateTime.UtcNow));
        RaiseDomainEvent(new PromotionCancelled(Guid.NewGuid(), DateTime.UtcNow, Id, cancelledBy));
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    private void EnsureMutable()
    {
        if (Status is PromotionStatus.Completed or PromotionStatus.Cancelled)
            throw new DomainException("PROMOTION_IMMUTABLE",
                $"Promotion is in terminal state '{Status}' and cannot be modified.");
    }

    private void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
