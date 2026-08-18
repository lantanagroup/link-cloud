namespace LantanaGroup.Link.Automation.Link.Models;

/// <summary>
/// Per-run, per-domain snapshot data stored in the database.
/// </summary>
public sealed class DomainSnapshot<T>
{
    public DateTimeOffset UpdatedAt { get; init; }
    public T Data { get; init; } = default!;
}

/// <summary>
/// Metadata about a test run.
/// </summary>
public sealed record RunSnapshotMeta
{
    public Guid RunId { get; init; }
    public string FacilityId { get; init; } = string.Empty;
    public string ReportId { get; init; } = string.Empty;
    public DateTimeOffset StartedAt { get; init; }
    public bool IsActive { get; init; } = true;
}

/// <summary>
/// Abstraction for persisting and reading automation run data.
/// Implementations can target MongoDB, SQL Server, or any other store.
///
/// Key concepts:
///   - Runs: metadata about each test run (lightweight, queryable)
///   - Domain snapshots: per-run, per-domain polling data (schedule, entries, etc.)
///   - Logs: full log output per run (potentially large)
/// </summary>
public interface ISnapshotStore
{
    // --- Run metadata ---
    Task RegisterRunAsync(Guid runId, RunSnapshotMeta meta, CancellationToken ct = default);
    Task UpdateRunMetaAsync(Guid runId, string facilityId, string reportId, CancellationToken ct = default);
    Task CompleteRunAsync(Guid runId, string? duration = null, CancellationToken ct = default);
    Task<IReadOnlyList<RunSnapshotMeta>> GetActiveRunsAsync(CancellationToken ct = default);
    Task<RunSnapshotMeta?> GetRunMetaAsync(Guid runId, CancellationToken ct = default);
    Task UpsertRunSummaryAsync(AutomationRunSummary summary, string? facilityId, string? reportId, CancellationToken ct = default);
    Task UpsertRunInputAsync(AutomationRunInputSnapshot input, CancellationToken ct = default);
    Task<AutomationRunSummary?> GetRunSummaryAsync(Guid runId, CancellationToken ct = default);
    Task<AutomationRunInputSnapshot?> GetRunInputAsync(Guid runId, CancellationToken ct = default);
    Task<PagedRunResult> GetRunsPageAsync(int pageNumber, int pageSize, string? sortBy = null, bool sortDescending = true, CancellationToken ct = default);
    Task<IReadOnlyList<AutomationRunSummary>> GetAllRunSummariesAsync(DateTimeOffset? since = null, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, ImportedBundleSnapshot>> GetImportedBundlesByIdsAsync(IEnumerable<Guid> bundleIds, CancellationToken ct = default);
    Task DeleteRunAsync(Guid runId, CancellationToken ct = default);

    // --- Domain snapshots (per-run, per-service polling data) ---
    Task SetDomainAsync<T>(Guid runId, string domain, T data, CancellationToken ct = default);
    Task<DomainSnapshot<T>?> GetDomainAsync<T>(Guid runId, string domain, CancellationToken ct = default);

    // --- Logs ---
    Task AppendLogsAsync(Guid runId, IReadOnlyList<string> newLines, CancellationToken ct = default);
    Task<List<string>> GetLogsAsync(Guid runId, CancellationToken ct = default);
}

public sealed record PagedRunResult(
    IReadOnlyList<AutomationRunSummary> Items,
    int PageNumber,
    int PageSize,
    long TotalCount);
