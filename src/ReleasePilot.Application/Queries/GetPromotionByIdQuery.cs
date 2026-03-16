using MediatR;
using ReleasePilot.Application.Interfaces;
using ReleasePilot.Application.ReadModels;
using ReleasePilot.Domain.Exceptions;

namespace ReleasePilot.Application.Queries;

public record GetPromotionByIdQuery(Guid PromotionId) : IRequest<PromotionDetailReadModel>;

public class GetPromotionByIdHandler : IRequestHandler<GetPromotionByIdQuery, PromotionDetailReadModel>
{
    private readonly IPromotionReadRepository _readRepository;

    public GetPromotionByIdHandler(IPromotionReadRepository readRepository)
    {
        _readRepository = readRepository;
    }

    public async Task<PromotionDetailReadModel> Handle(GetPromotionByIdQuery request, CancellationToken cancellationToken)
    {
        return await _readRepository.GetByIdAsync(request.PromotionId, cancellationToken)
            ?? throw new DomainException("PROMOTION_NOT_FOUND", $"Promotion '{request.PromotionId}' not found.");
    }
}
