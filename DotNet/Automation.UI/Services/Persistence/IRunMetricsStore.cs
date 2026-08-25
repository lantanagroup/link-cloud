namespace Automation.UI.Services.Persistence;

public interface IRunMetricsStore
{
    Task UpsertAsync(AutomationRunMetricsDocument document, CancellationToken cancellationToken = default);
    Task<AutomationRunMetricsDocument?> GetAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<AutomationRunMetricsDocument> Records, long TotalCount)> ListPageAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AutomationRunMetricsDocument>> ListSinceAsync(
        DateTimeOffset since,
        CancellationToken cancellationToken = default);
    Task<AutomationRunMetricsDocument?> GetPreviousSucceededAsync(
        Guid scenarioId,
        DateTimeOffset beforeFinishedAt,
        Guid excludeRunId,
        CancellationToken cancellationToken = default);
}
