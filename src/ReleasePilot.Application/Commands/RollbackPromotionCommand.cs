using MediatR;
using ReleasePilot.Application.Interfaces;
using ReleasePilot.Domain.Exceptions;

namespace ReleasePilot.Application.Commands;

public record RollbackPromotionCommand(
    Guid PromotionId,
    string Reason,
    string RolledBackBy) : IRequest;

public class RollbackPromotionHandler : IRequestHandler<RollbackPromotionCommand>
{
    private readonly IPromotionRepository _repository;
    private readonly IEventBus _eventBus;

    public RollbackPromotionHandler(IPromotionRepository repository, IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }

    public async Task Handle(RollbackPromotionCommand request, CancellationToken cancellationToken)
    {
        var promotion = await _repository.GetByIdAsync(request.PromotionId, cancellationToken)
            ?? throw new DomainException("PROMOTION_NOT_FOUND", $"Promotion '{request.PromotionId}' not found.");

        // Domain model enforces: must be InProgress, reason required
        promotion.Rollback(request.Reason, request.RolledBackBy);

        await _repository.UpdateAsync(promotion, cancellationToken);

        foreach (var domainEvent in promotion.DomainEvents)
            await _eventBus.PublishAsync(domainEvent, cancellationToken);

        promotion.ClearDomainEvents();
    }
}
