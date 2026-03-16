using MediatR;
using ReleasePilot.Application.Interfaces;
using ReleasePilot.Domain.Exceptions;

namespace ReleasePilot.Application.Commands;

public record CancelPromotionCommand(
    Guid PromotionId,
    string CancelledBy) : IRequest;

public class CancelPromotionHandler : IRequestHandler<CancelPromotionCommand>
{
    private readonly IPromotionRepository _repository;
    private readonly IEventBus _eventBus;

    public CancelPromotionHandler(IPromotionRepository repository, IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }

    public async Task Handle(CancelPromotionCommand request, CancellationToken cancellationToken)
    {
        var promotion = await _repository.GetByIdAsync(request.PromotionId, cancellationToken)
            ?? throw new DomainException("PROMOTION_NOT_FOUND", $"Promotion '{request.PromotionId}' not found.");

        // Domain model enforces: only from Requested state
        promotion.Cancel(request.CancelledBy);

        await _repository.UpdateAsync(promotion, cancellationToken);

        foreach (var domainEvent in promotion.DomainEvents)
            await _eventBus.PublishAsync(domainEvent, cancellationToken);

        promotion.ClearDomainEvents();
    }
}
