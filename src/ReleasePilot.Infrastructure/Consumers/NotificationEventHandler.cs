using MediatR;
using Microsoft.Extensions.Logging;
using ReleasePilot.Domain.Events;
using ReleasePilot.Domain.Ports;
using ReleasePilot.Application.Interfaces;

namespace ReleasePilot.Infrastructure.Consumers;

/// <summary>
/// Handles terminal-state domain events by sending notifications via the INotificationPort.
/// Triggered in-process via MediatR after event publication.
/// </summary>
public class NotificationEventHandler :
    INotificationHandler<PromotionCompleted>,
    INotificationHandler<PromotionRolledBack>,
    INotificationHandler<PromotionCancelled>
{
    private readonly INotificationPort _notificationPort;
    private readonly IPromotionRepository _repository;
    private readonly ILogger<NotificationEventHandler> _logger;

    public NotificationEventHandler(
        INotificationPort notificationPort,
        IPromotionRepository repository,
        ILogger<NotificationEventHandler> logger)
    {
        _notificationPort = notificationPort;
        _repository = repository;
        _logger = logger;
    }

    public async Task Handle(PromotionCompleted notification, CancellationToken cancellationToken)
    {
        var promotion = await _repository.GetByIdAsync(notification.PromotionId, cancellationToken);
        if (promotion is null) return;

        await _notificationPort.SendNotificationAsync(
            promotion.ApplicationName, promotion.Version, "Completed",
            $"Promotion of {promotion.ApplicationName} v{promotion.Version} to {promotion.TargetEnvironment} completed successfully.",
            cancellationToken);
    }

    public async Task Handle(PromotionRolledBack notification, CancellationToken cancellationToken)
    {
        var promotion = await _repository.GetByIdAsync(notification.PromotionId, cancellationToken);
        if (promotion is null) return;

        await _notificationPort.SendNotificationAsync(
            promotion.ApplicationName, promotion.Version, "RolledBack",
            $"Promotion of {promotion.ApplicationName} v{promotion.Version} to {promotion.TargetEnvironment} was rolled back. Reason: {notification.Reason}",
            cancellationToken);
    }

    public async Task Handle(PromotionCancelled notification, CancellationToken cancellationToken)
    {
        var promotion = await _repository.GetByIdAsync(notification.PromotionId, cancellationToken);
        if (promotion is null) return;

        await _notificationPort.SendNotificationAsync(
            promotion.ApplicationName, promotion.Version, "Cancelled",
            $"Promotion of {promotion.ApplicationName} v{promotion.Version} to {promotion.TargetEnvironment} was cancelled.",
            cancellationToken);
    }
}
