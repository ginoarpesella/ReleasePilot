namespace ReleasePilot.Domain.Enums;

public enum DeploymentEnvironment
{
    Dev = 0,
    Staging = 1,
    Production = 2
}

public static class DeploymentEnvironmentExtensions
{
    private static readonly DeploymentEnvironment[] PromotionOrder =
    [
        DeploymentEnvironment.Dev,
        DeploymentEnvironment.Staging,
        DeploymentEnvironment.Production
    ];

    public static DeploymentEnvironment? GetNextEnvironment(this DeploymentEnvironment current)
    {
        var index = Array.IndexOf(PromotionOrder, current);
        if (index < 0 || index >= PromotionOrder.Length - 1)
            return null;
        return PromotionOrder[index + 1];
    }

    public static bool IsValidPromotion(DeploymentEnvironment source, DeploymentEnvironment target)
    {
        return source.GetNextEnvironment() == target;
    }
}
