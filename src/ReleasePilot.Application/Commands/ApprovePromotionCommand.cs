using MediatR;
using ReleasePilot.Application.Interfaces;
using ReleasePilot.Domain.Exceptions;

namespace ReleasePilot.Application.Commands;

public record ApprovePromotionCommand(
    Guid PromotionId,
    string ApprovedBy,
    bool IsApprover) : IRequest;

public class ApprovePromotionHandler : IRequestHandler<ApprovePromotionCommand>
{
    private readonly IPromotionRepository _repository;
    private readonly IEventBus _eventBus;
    private readonly IUnitOfWork _unitOfWork;

    public ApprovePromotionHandler(IPromotionRepository repository, IEventBus eventBus, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _eventBus = eventBus;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(ApprovePromotionCommand request, CancellationToken cancellationToken)
    {
        var promotion = await _repository.GetByIdAsync(request.PromotionId, cancellationToken)
            ?? throw new DomainException("PROMOTION_NOT_FOUND", $"Promotion '{request.PromotionId}' not found.");

        var hasInProgress = await _repository.HasInProgressPromotionAsync(
            promotion.ApplicationName,
            promotion.TargetEnvironment.ToString(),
            cancellationToken);

        // Domain model enforces all invariants
        promotion.Approve(request.ApprovedBy, request.IsApprover, hasInProgress);

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
