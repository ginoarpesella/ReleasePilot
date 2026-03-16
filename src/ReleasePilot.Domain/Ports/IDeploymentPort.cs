namespace ReleasePilot.Domain.Ports;

public interface IDeploymentPort
{
    Task<bool> InitiateDeploymentAsync(string applicationName, string version, string targetEnvironment, CancellationToken cancellationToken = default);
}
