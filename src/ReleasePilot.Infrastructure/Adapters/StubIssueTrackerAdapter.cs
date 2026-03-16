using Microsoft.Extensions.Logging;
using ReleasePilot.Domain.Ports;

namespace ReleasePilot.Infrastructure.Adapters;

public class StubIssueTrackerAdapter : IIssueTrackerPort
{
    private readonly ILogger<StubIssueTrackerAdapter> _logger;

    // Simulated issue tracker data
    private static readonly Dictionary<string, WorkItemInfo> FakeWorkItems = new()
    {
        ["WI-101"] = new WorkItemInfo("WI-101", "Add user authentication", "Implement OAuth2 login flow with JWT tokens for API authentication.", "Done"),
        ["WI-102"] = new WorkItemInfo("WI-102", "Fix payment processing bug", "Fix race condition in concurrent payment processing that causes duplicate charges.", "Done"),
        ["WI-103"] = new WorkItemInfo("WI-103", "Update database schema", "BREAKING: Migrate from legacy schema to new normalized tables. Requires data migration.", "Done"),
        ["WI-104"] = new WorkItemInfo("WI-104", "Improve search performance", "Optimize full-text search queries by adding GIN indexes.", "In Progress"),
        ["WI-105"] = new WorkItemInfo("WI-105", "Add export feature", "Allow users to export reports as CSV and PDF.", "Done"),
        ["WI-106"] = new WorkItemInfo("WI-106", "Misc cleanup", "Various code cleanup tasks.", "Done"),
        ["WI-107"] = new WorkItemInfo("WI-107", "API rate limiting", "BREAKING: Implement rate limiting on all public API endpoints. Changes response headers.", "Done"),
        ["WI-108"] = new WorkItemInfo("WI-108", "Update dependencies", "Bump framework and library versions to latest stable.", "Done"),
    };

    public StubIssueTrackerAdapter(ILogger<StubIssueTrackerAdapter> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<WorkItemInfo>> GetWorkItemsAsync(IEnumerable<string> issueReferences, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[STUB ISSUE TRACKER] Retrieving work items: {References}", string.Join(", ", issueReferences));

        // Simulate API latency
        await Task.Delay(200, cancellationToken);

        var results = new List<WorkItemInfo>();
        foreach (var reference in issueReferences)
        {
            if (FakeWorkItems.TryGetValue(reference, out var workItem))
            {
                results.Add(workItem);
            }
            else
            {
                // Return a generic entry for unknown references
                results.Add(new WorkItemInfo(reference, $"Work item {reference}", "No description available.", "Unknown"));
            }
        }

        _logger.LogInformation("[STUB ISSUE TRACKER] Retrieved {Count} work items.", results.Count);
        return results;
    }
}
