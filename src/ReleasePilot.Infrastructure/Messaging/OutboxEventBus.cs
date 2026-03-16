using System.Text.Json;
using ReleasePilot.Application.Interfaces;
using ReleasePilot.Domain.Events;
using ReleasePilot.Infrastructure.Persistence;

namespace ReleasePilot.Infrastructure.Messaging;

/// <summary>
/// Implements the Outbox pattern: domain events are first persisted to the OutboxMessages table
/// within the same transaction as the aggregate changes, ensuring reliable event delivery.
/// A background processor then picks up unprocessed messages and publishes them to RabbitMQ.
/// </summary>
public class OutboxEventBus : IEventBus
{
    private readonly AppDbContext _context;

    public OutboxEventBus(AppDbContext context)
    {
        _context = context;
    }

    public async Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
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
        await _context.SaveChangesAsync(cancellationToken);
    }
}
