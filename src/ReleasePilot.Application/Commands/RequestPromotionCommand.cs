using MediatR;
using ReleasePilot.Application.Interfaces;
using ReleasePilot.Domain.Aggregates;
using ReleasePilot.Domain.Enums;

namespace ReleasePilot.Application.Commands;

public record RequestPromotionCommand(
    string ApplicationName,
    string Version,
    DeploymentEnvironment SourceEnvironment,
    DeploymentEnvironment TargetEnvironment,
    string RequestedBy,
    List<string>? WorkItemReferences = null) : IRequest<Guid>;

public class RequestPromotionHandler : IRequestHandler<RequestPromotionCommand, Guid>
{
    private readonly IPromotionRepository _repository;
    private readonly IEventBus _eventBus;
    private readonly IUnitOfWork _unitOfWork;

    public RequestPromotionHandler(IPromotionRepository repository, IEventBus eventBus, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _eventBus = eventBus;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(RequestPromotionCommand request, CancellationToken cancellationToken)
    {
        // Domain model enforces all invariants (valid promotion path, required fields)
        var promotion = Promotion.Request(
            request.ApplicationName,
            request.Version,
            request.SourceEnvironment,
            request.TargetEnvironment,
            request.RequestedBy,
            request.WorkItemReferences);

        await _repository.AddAsync(promotion, cancellationToken);

        foreach (var domainEvent in promotion.DomainEvents)
            await _eventBus.PublishAsync(domainEvent, cancellationToken);

        // Atomic commit: aggregate + outbox messages in a single transaction
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Dispatch in-process notifications after transaction commits
        await _eventBus.DispatchPendingAsync(cancellationToken);

        promotion.ClearDomainEvents();
        return promotion.Id;
    }
}
