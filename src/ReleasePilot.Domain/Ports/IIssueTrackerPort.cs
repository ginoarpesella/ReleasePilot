namespace ReleasePilot.Domain.Ports;

public record WorkItemInfo(string Id, string Title, string Description, string Status);

public interface IIssueTrackerPort
{
    Task<IReadOnlyList<WorkItemInfo>> GetWorkItemsAsync(IEnumerable<string> issueReferences, CancellationToken cancellationToken = default);
}
