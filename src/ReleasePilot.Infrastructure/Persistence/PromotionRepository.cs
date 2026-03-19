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

    public async Task UpdateAsync(Promotion promotion, CancellationToken cancellationToken = default)
    {
        // Persist new state transitions
        foreach (var transition in promotion.StateHistory)
        {
            var exists = await _context.StateTransitions
                .AnyAsync(s => s.PromotionId == promotion.Id
                    && s.ToStatus == transition.ToStatus.ToString()
                    && s.TransitionedAt == transition.TransitionedAt,
                    cancellationToken);

            if (!exists)
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

        _context.Promotions.Update(promotion);
    }

    public async Task<bool> HasInProgressPromotionAsync(string applicationName, string targetEnvironment, CancellationToken cancellationToken = default)
    {
        return await _context.Promotions.AnyAsync(
            p => p.ApplicationName == applicationName
                && p.TargetEnvironment.ToString() == targetEnvironment
                && p.Status == PromotionStatus.InProgress,
            cancellationToken);
    }
}
