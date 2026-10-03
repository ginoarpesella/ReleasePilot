using Microsoft.EntityFrameworkCore;
using ReleasePilot.Application.Interfaces;
using ReleasePilot.Application.ReadModels;
using ReleasePilot.Domain.Enums;

namespace ReleasePilot.Infrastructure.Persistence;

public class PromotionReadRepository : IPromotionReadRepository
{
    private readonly AppDbContext _context;

    public PromotionReadRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PromotionDetailReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var promotion = await _context.Promotions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (promotion is null) return null;

        var stateHistory = await _context.StateTransitions
            .AsNoTracking()
            .Where(s => s.PromotionId == id)
            .OrderBy(s => s.TransitionedAt)
            .Select(s => new StateTransitionReadModel(
                s.FromStatus, s.ToStatus, s.ActingUser, s.TransitionedAt, s.Reason))
            .ToListAsync(cancellationToken);

        return new PromotionDetailReadModel(
            promotion.Id,
            promotion.ApplicationName,
            promotion.Version,
            promotion.SourceEnvironment.ToString(),
            promotion.TargetEnvironment.ToString(),
            promotion.Status.ToString(),
            promotion.RequestedBy,
            promotion.CreatedAt,
            promotion.CompletedAt,
            promotion.RollbackReason,
            promotion.WorkItemReferences,
            stateHistory);
    }

    public async Task<EnvironmentStatusReadModel> GetEnvironmentStatusAsync(
        string applicationName, CancellationToken cancellationToken = default)
    {
        // Single round trip: latest promotion per target environment
        var latestByEnvironment = await _context.Promotions
            .AsNoTracking()
            .Where(p => p.ApplicationName == applicationName)
            .GroupBy(p => p.TargetEnvironment)
            .Select(g => g
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new { p.TargetEnvironment, p.Id, p.Version, p.Status, p.CreatedAt })
                .First())
            .ToDictionaryAsync(p => p.TargetEnvironment, cancellationToken);

        var environments = Enum.GetValues<DeploymentEnvironment>()
            .Select(env =>
            {
                var latest = latestByEnvironment.GetValueOrDefault(env);
                return new EnvironmentPromotionStatus(
                    env.ToString(),
                    latest?.Id,
                    latest?.Version,
                    latest?.Status.ToString(),
                    latest?.CreatedAt);
            })
            .ToList();

        return new EnvironmentStatusReadModel(applicationName, environments);
    }

    public async Task<PaginatedResult<PromotionSummaryReadModel>> ListByApplicationAsync(
        string applicationName, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Promotions
            .AsNoTracking()
            .Where(p => p.ApplicationName == applicationName)
            .OrderByDescending(p => p.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PromotionSummaryReadModel(
                p.Id,
                p.Version,
                p.SourceEnvironment.ToString(),
                p.TargetEnvironment.ToString(),
                p.Status.ToString(),
                p.RequestedBy,
                p.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PaginatedResult<PromotionSummaryReadModel>(items, totalCount, page, pageSize);
    }
}
