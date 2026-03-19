using System.Text.Json;
using MediatR;
using ReleasePilot.Application.Interfaces;
using ReleasePilot.Domain.Events;
using ReleasePilot.Infrastructure.Persistence;

namespace ReleasePilot.Infrastructure.Messaging;

/// <summary>
/// Implements the Outbox pattern: domain events are persisted to the OutboxMessages table
/// within the same unit of work as the aggregate changes, ensuring atomicity.
/// A background processor then picks up unprocessed messages and publishes them to RabbitMQ.
/// In-process MediatR notifications are dispatched after the transaction commits.
/// </summary>
public class OutboxEventBus : IEventBus
{
    private readonly AppDbContext _context;
    private readonly IMediator _mediator;
    private readonly List<IDomainEvent> _pendingEvents = [];

    public OutboxEventBus(AppDbContext context, IMediator mediator)
    {
        _context = context;
        _mediator = mediator;
    }

    public Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var outboxMessage = new OutboxMessage
        {
            Id = domainEvent.EventId,
            EventType = domainEvent.EventType,
            Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
            CreatedAt = DateTime.UtcNow,
            IsProcessed = false
        };

        _context.OutboxMessages.Add(outboxMessage);
        _pendingEvents.Add(domainEvent);
        return Task.CompletedTask;
    }

    public async Task DispatchPendingAsync(CancellationToken cancellationToken = default)
    {
        var events = _pendingEvents.ToList();
        _pendingEvents.Clear();

        foreach (var domainEvent in events)
            await _mediator.Publish(domainEvent, cancellationToken);
    }
}
