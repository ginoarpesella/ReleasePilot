using ReleasePilot.Domain.Events;

namespace ReleasePilot.Application.Interfaces;

public interface IEventBus
{
    Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}
