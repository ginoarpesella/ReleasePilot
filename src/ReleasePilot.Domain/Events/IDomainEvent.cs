using MediatR;

namespace ReleasePilot.Domain.Events;

public interface IDomainEvent : INotification
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
    string EventType { get; }
}
