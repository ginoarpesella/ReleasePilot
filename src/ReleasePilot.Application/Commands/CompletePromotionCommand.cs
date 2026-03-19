using MediatR;
using ReleasePilot.Application.Interfaces;
using ReleasePilot.Domain.Exceptions;

namespace ReleasePilot.Application.Commands;

public record CompletePromotionCommand(Guid PromotionId) : IRequest;

public class CompletePromotionHandler : IRequestHandler<CompletePromotionCommand>
{
    private readonly IPromotionRepository _repository;
    private readonly IEventBus _eventBus;
    private readonly IUnitOfWork _unitOfWork;

    public CompletePromotionHandler(IPromotionRepository repository, IEventBus eventBus, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _eventBus = eventBus;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(CompletePromotionCommand request, CancellationToken cancellationToken)
    {
        var promotion = await _repository.GetByIdAsync(request.PromotionId, cancellationToken)
            ?? throw new DomainException("PROMOTION_NOT_FOUND", $"Promotion '{request.PromotionId}' not found.");

        // Domain model enforces: must be InProgress
        promotion.Complete();

        await _repository.UpdateAsync(promotion, cancellationToken);

        foreach (var domainEvent in promotion.DomainEvents)
            await _eventBus.PublishAsync(domainEvent, cancellationToken);

        // Atomic commit: aggregate + outbox messages in a single transaction
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Dispatch in-process notifications after transaction commits
        await _eventBus.DispatchPendingAsync(cancellationToken);

        promotion.ClearDomainEvents();
    }
}
