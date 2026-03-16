using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ReleasePilot.Infrastructure.Messaging;
using ReleasePilot.Infrastructure.Persistence;

namespace ReleasePilot.Infrastructure.Consumers;

/// <summary>
/// Consumes domain events from RabbitMQ and persists them as audit log entries.
/// This runs as a decoupled background service — the API returns before this consumer
/// finishes processing (fire-and-forget pattern).
/// </summary>
public class AuditLogConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditLogConsumer> _logger;
    private readonly string _connectionString;

    public AuditLogConsumer(
        IServiceScopeFactory scopeFactory,
        ILogger<AuditLogConsumer> logger,
        RabbitMqSettings settings)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _connectionString = settings.ConnectionString;
    }

    private static readonly ResiliencePipeline RetryPipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = int.MaxValue,
            Delay = TimeSpan.FromSeconds(5),
            BackoffType = DelayBackoffType.Exponential,
            MaxDelay = TimeSpan.FromMinutes(2),
            ShouldHandle = new PredicateBuilder().Handle<Exception>()
        })
        .Build();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connection = await RetryPipeline.ExecuteAsync(async ct =>
        {
            _logger.LogInformation("AuditLogConsumer: Attempting to connect to RabbitMQ...");
            var factory = new ConnectionFactory { Uri = new Uri(_connectionString) };
            return await factory.CreateConnectionAsync(ct);
        }, stoppingToken);

        using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(OutboxProcessor.QueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                var eventType = ea.BasicProperties.Type ?? "Unknown";
                var messageId = ea.BasicProperties.MessageId;

                // Parse the event payload to extract promotionId and acting user
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                var promotionId = root.TryGetProperty("PromotionId", out var pidProp)
                    ? pidProp.GetGuid()
                    : Guid.Empty;

                var actingUser = ExtractActingUser(root, eventType);

                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var auditEntry = new AuditLogEntry
                {
                    Id = Guid.NewGuid(),
                    EventType = eventType,
                    PromotionId = promotionId,
                    ActingUser = actingUser,
                    Timestamp = DateTime.UtcNow,
                    Payload = body
                };

                context.AuditLogEntries.Add(auditEntry);
                await context.SaveChangesAsync(stoppingToken);

                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, stoppingToken);
                _logger.LogInformation("Audit log entry created for event {EventType}, Promotion {PromotionId}", eventType, promotionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing audit log message");
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(OutboxProcessor.QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

        _logger.LogInformation("AuditLogConsumer started. Listening on queue: {Queue}", OutboxProcessor.QueueName);

        // Keep running until cancellation
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private static string ExtractActingUser(JsonElement root, string eventType)
    {
        return eventType switch
        {
            "PromotionRequested" => root.TryGetProperty("RequestedBy", out var rb) ? rb.GetString() ?? "unknown" : "unknown",
            "PromotionApproved" => root.TryGetProperty("ApprovedBy", out var ab) ? ab.GetString() ?? "unknown" : "unknown",
            "DeploymentStarted" => root.TryGetProperty("StartedBy", out var sb) ? sb.GetString() ?? "unknown" : "unknown",
            "PromotionRolledBack" => root.TryGetProperty("RolledBackBy", out var rbb) ? rbb.GetString() ?? "unknown" : "unknown",
            "PromotionCancelled" => root.TryGetProperty("CancelledBy", out var cb) ? cb.GetString() ?? "unknown" : "unknown",
            _ => "system"
        };
    }
}
