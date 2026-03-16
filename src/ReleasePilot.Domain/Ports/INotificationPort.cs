namespace ReleasePilot.Domain.Ports;

public interface INotificationPort
{
    Task SendNotificationAsync(string applicationName, string version, string status, string message, CancellationToken cancellationToken = default);
}
