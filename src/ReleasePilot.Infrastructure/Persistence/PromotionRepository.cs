using Microsoft.EntityFrameworkCore;
using ReleasePilot.Application.Interfaces;
using ReleasePilot.Domain.Aggregates;
using ReleasePilot.Domain.Enums;

namespace ReleasePilot.Infrastructure.Persistence;

public class PromotionRepository : IPromotionRepository
{
    private readonly AppDbContext _context;

    public PromotionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Promotion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Promotions.FindAsync([id], cancellationToken);
    }

    public async Task AddAsync(Promotion promotion, CancellationToken cancellationToken = default)
    {
        // Persist state history as separate entities
        foreach (var transition in promotion.StateHistory)
        {
            _context.StateTransitions.Add(new StateTransitionEntity
            {
                Id = Guid.NewGuid(),
                PromotionId = promotion.Id,
                FromStatus = transition.FromStatus.ToString(),
                ToStatus = transition.ToStatus.ToString(),
                ActingUser = transition.ActingUser,
                TransitionedAt = transition.TransitionedAt,
                Reason = transition.Reason
            });
        }

        _context.Promotions.Add(promotion);
    }

    public Task UpdateAsync(Promotion promotion, CancellationToken cancellationToken = default)
    {
        // Persist new state transitions. StateHistory is not loaded from the database, so it only
        // holds transitions raised in this scope; dedupe against the change tracker instead of querying.
        var tracked = _context.StateTransitions.Local
            .Where(s => s.PromotionId == promotion.Id)
            .Select(s => (s.ToStatus, s.TransitionedAt))
            .ToHashSet();

        foreach (var transition in promotion.StateHistory)
        {
            if (tracked.Add((transition.ToStatus.ToString(), transition.TransitionedAt)))
            {
                _context.StateTransitions.Add(new StateTransitionEntity
                {
                    Id = Guid.NewGuid(),
                    PromotionId = promotion.Id,
                    FromStatus = transition.FromStatus.ToString(),
                    ToStatus = transition.ToStatus.ToString(),
                    ActingUser = transition.ActingUser,
                    TransitionedAt = transition.TransitionedAt,
                    Reason = transition.Reason
                });
            }
        }

        // Tracked entities are saved via change detection (only modified columns);
        // Update() would mark every column dirty.
        if (_context.Entry(promotion).State == EntityState.Detached)
            _context.Promotions.Update(promotion);

        return Task.CompletedTask;
    }

    public async Task<bool> HasInProgressPromotionAsync(string applicationName, string targetEnvironment, CancellationToken cancellationToken = default)
    {
        // Compare on the mapped enum (not ToString()) so the composite index can be used
        var environment = Enum.Parse<DeploymentEnvironment>(targetEnvironment);

        return await _context.Promotions.AnyAsync(
            p => p.ApplicationName == applicationName
                && p.TargetEnvironment == environment
                && p.Status == PromotionStatus.InProgress,
            cancellationToken);
    }
}
