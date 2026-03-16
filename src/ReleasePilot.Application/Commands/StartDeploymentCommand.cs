using MediatR;
using ReleasePilot.Application.Interfaces;
using ReleasePilot.Domain.Exceptions;
using ReleasePilot.Domain.Ports;

namespace ReleasePilot.Application.Commands;

public record StartDeploymentCommand(
    Guid PromotionId,
    string StartedBy) : IRequest;

public class StartDeploymentHandler : IRequestHandler<StartDeploymentCommand>
{
    private readonly IPromotionRepository _repository;
    private readonly IEventBus _eventBus;
    private readonly IDeploymentPort _deploymentPort;

    public StartDeploymentHandler(
        IPromotionRepository repository,
        IEventBus eventBus,
        IDeploymentPort deploymentPort)
    {
        _repository = repository;
        _eventBus = eventBus;
        _deploymentPort = deploymentPort;
    }

    public async Task Handle(StartDeploymentCommand request, CancellationToken cancellationToken)
    {
        var promotion = await _repository.GetByIdAsync(request.PromotionId, cancellationToken)
            ?? throw new DomainException("PROMOTION_NOT_FOUND", $"Promotion '{request.PromotionId}' not found.");

        // Domain model enforces: must be in Approved state
        promotion.StartDeployment(request.StartedBy);

        // Call the deployment port (external system)
        await _deploymentPort.InitiateDeploymentAsync(
            promotion.ApplicationName,
            promotion.Version,
            promotion.TargetEnvironment.ToString(),
            cancellationToken);

        await _repository.UpdateAsync(promotion, cancellationToken);

        foreach (var domainEvent in promotion.DomainEvents)
            await _eventBus.PublishAsync(domainEvent, cancellationToken);

        promotion.ClearDomainEvents();
    }
}
