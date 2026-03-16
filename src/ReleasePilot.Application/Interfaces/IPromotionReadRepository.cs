using ReleasePilot.Application.ReadModels;

namespace ReleasePilot.Application.Interfaces;

public interface IPromotionReadRepository
{
    Task<PromotionDetailReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EnvironmentStatusReadModel> GetEnvironmentStatusAsync(string applicationName, CancellationToken cancellationToken = default);
    Task<PaginatedResult<PromotionSummaryReadModel>> ListByApplicationAsync(string applicationName, int page, int pageSize, CancellationToken cancellationToken = default);
}

public record PaginatedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
