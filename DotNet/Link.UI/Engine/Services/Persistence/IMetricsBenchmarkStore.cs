namespace Automation.UI.Services.Persistence;

public interface IMetricsBenchmarkStore
{
    Task UpsertAsync(AutomationMetricsBenchmarkDocument document, CancellationToken cancellationToken = default);
    Task<AutomationMetricsBenchmarkDocument?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<AutomationMetricsBenchmarkDocument> Records, long TotalCount)> ListPageAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
