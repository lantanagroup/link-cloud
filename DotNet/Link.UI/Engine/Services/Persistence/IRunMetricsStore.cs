namespace Automation.UI.Services.Persistence;

public interface IRunMetricsStore
{
    Task UpsertAsync(AutomationRunMetricsDocument document, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<AutomationRunMetricsDocument?> GetAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<AutomationRunMetricsDocument> Records, long TotalCount)> ListPageAsync(
        int pageNumber,
        int pageSize,
        Guid? scenarioId = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AutomationRunMetricsDocument>> ListSinceAsync(
        DateTimeOffset since,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AutomationRunMetricsDocument>> ListByScenarioAsync(
        Guid scenarioId,
        CancellationToken cancellationToken = default);
    Task<AutomationRunMetricsDocument?> GetPreviousAsync(
        Guid scenarioId,
        DateTimeOffset beforeFinishedAt,
        Guid excludeRunId,
        CancellationToken cancellationToken = default);
    Task<AutomationRunMetricsDocument?> GetPreviousSucceededAsync(
        Guid scenarioId,
        DateTimeOffset beforeFinishedAt,
        Guid excludeRunId,
        CancellationToken cancellationToken = default);
    Task<AutomationRunMetricsDocument?> GetPreviousSucceededSameFingerprintAsync(
        Guid scenarioId,
        string fingerprint,
        DateTimeOffset beforeFinishedAt,
        Guid excludeRunId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Newest succeeded snapshots for one scenario, limited so a regression check never walks the whole history.
    /// An empty fingerprint matches any succeeded snapshot, the same rule as the single-document lookup.
    /// </summary>
    Task<IReadOnlyList<AutomationRunMetricsDocument>> ListRecentSucceededSameFingerprintAsync(
        Guid scenarioId,
        string fingerprint,
        DateTimeOffset beforeFinishedAt,
        Guid excludeRunId,
        int limit,
        CancellationToken cancellationToken = default);
}
