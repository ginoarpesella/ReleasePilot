using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using ReleasePilot.Infrastructure.Persistence;

namespace ReleasePilot.Infrastructure.Messaging;

/// <summary>
/// Background service that polls the Outbox table for unprocessed messages
/// and publishes them to RabbitMQ. This decouples the write side from the message broker,
/// ensuring at-least-once delivery semantics.
/// </summary>
public class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly string _connectionString;

    public const string ExchangeName = "releasepilot.events";
    public const string QueueName = "releasepilot.audit";

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessor> logger,
        RabbitMqSettings settings)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _connectionString = settings.ConnectionString;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for RabbitMQ to be available
        IConnection? connection = null;
        var retryCount = 0;
        while (!stoppingToken.IsCancellationRequested && connection is null)
        {
            try
            {
                var factory = new ConnectionFactory { Uri = new Uri(_connectionString) };
                connection = await factory.CreateConnectionAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                retryCount++;
                _logger.LogWarning(ex, "Failed to connect to RabbitMQ (attempt {Attempt}). Retrying in 5s...", retryCount);
                await Task.Delay(5000, stoppingToken);
            }
        }

        if (connection is null) return;

        using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Fanout, durable: true, cancellationToken: stoppingToken);
        await channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
        await channel.QueueBindAsync(QueueName, ExchangeName, routingKey: "", cancellationToken: stoppingToken);

        _logger.LogInformation("Outbox processor started. Exchange: {Exchange}, Queue: {Queue}", ExchangeName, QueueName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var messages = await context.OutboxMessages
                    .Where(m => !m.IsProcessed)
                    .OrderBy(m => m.CreatedAt)
                    .Take(50)
                    .ToListAsync(stoppingToken);

                foreach (var message in messages)
                {
                    var body = Encoding.UTF8.GetBytes(message.Payload);
                    var properties = new BasicProperties
                    {
                        ContentType = "application/json",
                        DeliveryMode = DeliveryModes.Persistent,
                        MessageId = message.Id.ToString(),
                        Type = message.EventType,
                        Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                    };

                    await channel.BasicPublishAsync(ExchangeName, routingKey: message.EventType, mandatory: false, properties, body, stoppingToken);

                    message.IsProcessed = true;
                    message.ProcessedAt = DateTime.UtcNow;
                    await context.SaveChangesAsync(stoppingToken);

                    _logger.LogInformation("Published outbox message {MessageId} of type {EventType}", message.Id, message.EventType);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages");
            }

            await Task.Delay(1000, stoppingToken);
        }
    }
}

public class RabbitMqSettings
{
    public string ConnectionString { get; set; } = "amqp://guest:guest@localhost:5672/";
}
