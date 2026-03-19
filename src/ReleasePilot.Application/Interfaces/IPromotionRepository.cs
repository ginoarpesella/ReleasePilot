using ReleasePilot.Domain.Aggregates;

namespace ReleasePilot.Application.Interfaces;

public interface IPromotionRepository
{
    Task<Promotion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Promotion promotion, CancellationToken cancellationToken = default);
    Task UpdateAsync(Promotion promotion, CancellationToken cancellationToken = default);
    Task<bool> HasInProgressPromotionAsync(string applicationName, string targetEnvironment, CancellationToken cancellationToken = default);
}
