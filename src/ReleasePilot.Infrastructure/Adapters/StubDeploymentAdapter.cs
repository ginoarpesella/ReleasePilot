using Microsoft.Extensions.Logging;
using ReleasePilot.Domain.Ports;

namespace ReleasePilot.Infrastructure.Adapters;

public class StubDeploymentAdapter : IDeploymentPort
{
    private readonly ILogger<StubDeploymentAdapter> _logger;

    public StubDeploymentAdapter(ILogger<StubDeploymentAdapter> logger)
    {
        _logger = logger;
    }

    public async Task<bool> InitiateDeploymentAsync(string applicationName, string version, string targetEnvironment, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[STUB DEPLOYMENT] Initiating deployment of {Application} v{Version} to {Environment}...",
            applicationName, version, targetEnvironment);

        // Simulate deployment latency
        await Task.Delay(500, cancellationToken);

        _logger.LogInformation(
            "[STUB DEPLOYMENT] Deployment of {Application} v{Version} to {Environment} initiated successfully.",
            applicationName, version, targetEnvironment);

        return true;
    }
}
