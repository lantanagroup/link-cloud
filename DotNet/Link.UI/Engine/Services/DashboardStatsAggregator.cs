using Automation.UI.Models;
using Automation.UI.Services.Persistence;

namespace Automation.UI.Services;

/// <summary>
/// Builds the rolling-14-day dashboard statistics from the persisted run history,
/// merging in any live in-memory runs that haven't yet been upserted to the store.
/// Extracted from <see cref="AutomationRunManager"/> so the aggregation rules
/// (status counts, success rate, average duration, per-day histogram) can be
/// covered by focused unit tests independent of the full run-manager fixture.
/// </summary>
public sealed class DashboardStatsAggregator
{
    private const int WindowDays = 14;

    private readonly ISnapshotStore _store;

    public DashboardStatsAggregator(ISnapshotStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Builds the dashboard stats. <paramref name="inMemoryRuns"/> is merged on top
    /// of the persisted set so a brand-new run that hasn't yet round-tripped through
    /// the store still shows up on the KPI cards.
    /// </summary>
    public async Task<RunDashboardStats> BuildAsync(
        IEnumerable<AutomationRunSummary> inMemoryRuns,
        CancellationToken cancellationToken = default)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-WindowDays);
        var allRuns = await _store.GetAllRunSummariesAsync(since, cancellationToken);

        var inMemorySnapshot = inMemoryRuns?.ToList() ?? [];
        var persistedRunIds = new HashSet<Guid>(allRuns.Select(r => r.RunId));
        var merged = allRuns
            .Concat(inMemorySnapshot.Where(m => !persistedRunIds.Contains(m.RunId)))
            .ToList();

        var stats = new RunDashboardStats
        {
            TotalRuns = merged.Count,
            Succeeded = merged.Count(r => r.Status == AutomationRunStatus.Succeeded),
            Failed = merged.Count(r => r.Status == AutomationRunStatus.Failed),
            Cancelled = merged.Count(r => r.Status == AutomationRunStatus.Cancelled),
            Running = merged.Count(r => r.Status.IsInProgress()),
            Queued = merged.Count(r => r.Status == AutomationRunStatus.Queued),
        };

        var completedWithDuration = merged
            .Where(r => r.Status is AutomationRunStatus.Succeeded or AutomationRunStatus.Failed && r.FinishedAt.HasValue)
            .Select(r => (r.FinishedAt!.Value - r.CreatedAt).TotalSeconds)
            .Where(d => d > 0)
            .ToList();

        stats.AvgDurationSeconds = completedWithDuration.Count > 0
            ? Math.Round(completedWithDuration.Average(), 1)
            : 0;

        // Last 14 days histogram (inclusive of today).
        var cutoff = DateTimeOffset.UtcNow.AddDays(-(WindowDays - 1)).Date;
        for (var d = cutoff; d <= DateTime.UtcNow.Date; d = d.AddDays(1))
            stats.RunsPerDay[d.ToString("yyyy-MM-dd")] = new RunDayBucket();

        foreach (var run in merged.Where(r => r.CreatedAt.Date >= cutoff))
        {
            var key = run.CreatedAt.Date.ToString("yyyy-MM-dd");
            if (!stats.RunsPerDay.TryGetValue(key, out var bucket))
            {
                bucket = new RunDayBucket();
                stats.RunsPerDay[key] = bucket;
            }

            switch (run.Status)
            {
                case AutomationRunStatus.Succeeded: bucket.Succeeded++; break;
                case AutomationRunStatus.Failed: bucket.Failed++; break;
                case AutomationRunStatus.Cancelled: bucket.Cancelled++; break;
                default: bucket.Other++; break;
            }
        }

        return stats;
    }
}
