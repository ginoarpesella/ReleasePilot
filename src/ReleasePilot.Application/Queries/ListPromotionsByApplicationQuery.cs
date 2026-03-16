using MediatR;
using ReleasePilot.Application.Interfaces;
using ReleasePilot.Application.ReadModels;

namespace ReleasePilot.Application.Queries;

public record ListPromotionsByApplicationQuery(
    string ApplicationName,
    int Page = 1,
    int PageSize = 20) : IRequest<PaginatedResult<PromotionSummaryReadModel>>;

public class ListPromotionsByApplicationHandler
    : IRequestHandler<ListPromotionsByApplicationQuery, PaginatedResult<PromotionSummaryReadModel>>
{
    private readonly IPromotionReadRepository _readRepository;

    public ListPromotionsByApplicationHandler(IPromotionReadRepository readRepository)
    {
        _readRepository = readRepository;
    }

    public async Task<PaginatedResult<PromotionSummaryReadModel>> Handle(
        ListPromotionsByApplicationQuery request,
        CancellationToken cancellationToken)
    {
        return await _readRepository.ListByApplicationAsync(
            request.ApplicationName,
            request.Page,
            request.PageSize,
            cancellationToken);
    }
}
