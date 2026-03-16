using MediatR;
using Microsoft.Extensions.Logging;
using ReleasePilot.Agent;
using ReleasePilot.Application.Interfaces;
using ReleasePilot.Domain.Events;

namespace ReleasePilot.Infrastructure.Consumers;

/// <summary>
/// Event handler that triggers the AI Release Notes Agent when a promotion is approved.
/// Wired into the domain event lifecycle via MediatR.
/// </summary>
public class ReleaseNotesAgentHandler : INotificationHandler<PromotionApproved>
{
    private readonly ReleaseNotesAgent _agent;
    private readonly IPromotionRepository _repository;
    private readonly ILogger<ReleaseNotesAgentHandler> _logger;

    public ReleaseNotesAgentHandler(
        ReleaseNotesAgent agent,
        IPromotionRepository repository,
        ILogger<ReleaseNotesAgentHandler> logger)
    {
        _agent = agent;
        _repository = repository;
        _logger = logger;
    }

    public async Task Handle(PromotionApproved notification, CancellationToken cancellationToken)
    {
        try
        {
            var promotion = await _repository.GetByIdAsync(notification.PromotionId, cancellationToken);
            if (promotion is null)
            {
                _logger.LogWarning("Promotion {PromotionId} not found for release notes generation", notification.PromotionId);
                return;
            }

            if (promotion.WorkItemReferences.Count == 0)
            {
                _logger.LogInformation("No work items referenced in promotion {PromotionId}. Skipping release notes.", notification.PromotionId);
                return;
            }

            var draft = await _agent.GenerateReleaseNotesAsync(
                promotion.Id,
                promotion.ApplicationName,
                promotion.Version,
                promotion.WorkItemReferences,
                cancellationToken);

            _logger.LogInformation(
                "Release notes generated for promotion {PromotionId}: {Summary}",
                notification.PromotionId, draft.Summary);
        }
        catch (Exception ex)
        {
            // Agent failures should not block the promotion workflow
            _logger.LogError(ex, "Failed to generate release notes for promotion {PromotionId}", notification.PromotionId);
        }
    }
}
