using Microsoft.Extensions.Logging;
using ReleasePilot.Domain.Ports;

namespace ReleasePilot.Infrastructure.Adapters;

public class StubNotificationAdapter : INotificationPort
{
    private readonly ILogger<StubNotificationAdapter> _logger;

    public StubNotificationAdapter(ILogger<StubNotificationAdapter> logger)
    {
        _logger = logger;
    }

    public async Task SendNotificationAsync(string applicationName, string version, string status, string message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[STUB NOTIFICATION] Sending notification for {Application} v{Version}: Status={Status}, Message={Message}",
            applicationName, version, status, message);

        await Task.Delay(100, cancellationToken);

        _logger.LogInformation("[STUB NOTIFICATION] Notification sent successfully.");
    }
}
