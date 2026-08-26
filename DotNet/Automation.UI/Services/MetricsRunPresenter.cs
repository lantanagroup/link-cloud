using Automation.UI.Models;
using Automation.UI.Models.Metrics;
using Automation.UI.Services.Persistence;
using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace Automation.UI.Services;

public sealed class MetricsRunPresenter
{
    public const int WindowDays = 14;

    private readonly IRunMetricsStore _store;
    private readonly IAutomationRunManager _runManager;
    private readonly IScenarioStore _scenarioStore;
    private readonly IMetricsBenchmarkStore _benchmarks;

    public MetricsRunPresenter(
        IRunMetricsStore store,
        IAutomationRunManager runManager,
        IScenarioStore scenarioStore,
        IMetricsBenchmarkStore? benchmarks = null)
    {
        _store = store;
        _runManager = runManager;
        _scenarioStore = scenarioStore;
        _benchmarks = benchmarks ?? new EmptyBenchmarkStore();
    }

    public async Task<(IReadOnlyList<MetricsRunListItem> Records, PaginationMetadata Metadata)> ListAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var (records, total) = await _store.ListPageAsync(pageNumber, pageSize, cancellationToken);
        return (records.Select(ToListItem).ToList(), new PaginationMetadata(pageSize, pageNumber, total));
    }

    public async Task<MetricsDashboardViewModel> GetDashboardAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (records, metadata) = await ListAsync(pageNumber, pageSize, cancellationToken);
        var since = DateTimeOffset.UtcNow.AddDays(-WindowDays);
        var recent = await _store.ListSinceAsync(since, cancellationToken);
        var lastDoc = recent.FirstOrDefault();

        var scenarios = (await _scenarioStore.GetAllAsync(cancellationToken))
            .Where(s => s.IsMetricsRun)
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new MetricsDashboardViewModel
        {
            LastRunE2eSeconds = lastDoc?.E2eDurationSeconds ?? records.FirstOrDefault()?.E2eDurationSeconds ?? 0,
            LastRunPatientsPerMinute = lastDoc?.Throughput.PatientsPerMinute,
            LastRunStagesUnavailable = lastDoc == null
                ? records.FirstOrDefault()?.StagesUnavailable ?? true
                : AreStagesUnavailable(lastDoc),
            RegressionFlagCount = recent.Sum(d => d.Regression.Flags.Count),
            Runs = records,
            Metadata = metadata,
            MetricsScenarios = scenarios,
            DurationTrend = recent
                .OrderBy(d => d.FinishedAt)
                .Select(d => new MetricsDurationPoint
                {
                    RunId = d.RunId,
                    FinishedAt = d.FinishedAt,
                    E2eDurationSeconds = d.E2eDurationSeconds
                })
                .ToList()
        };
    }

    public async Task<MetricsRunDetailViewModel?> GetDetailAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await _runManager.GetRunAsync(runId, cancellationToken);
        if (run == null)
            return null;

        var snapshot = await _store.GetAsync(runId, cancellationToken);
        if (snapshot == null)
            return UnavailableFromRun(run);

        var detail = ToDetail(snapshot);
        if (detail.PreviousRunId == null && snapshot.ScenarioId is Guid scenarioId && scenarioId != Guid.Empty)
        {
            var previous = await _store.GetPreviousSucceededAsync(
                scenarioId,
                snapshot.FinishedAt,
                snapshot.RunId,
                cancellationToken);
            detail.PreviousRunId = previous?.RunId;
        }

        return detail;
    }

    public async Task<MetricsRunDetailViewModel?> GetCapturedAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.GetAsync(runId, cancellationToken);
        return snapshot == null ? null : ToDetail(snapshot);
    }

    public async Task<(IReadOnlyList<AutomationMetricsBenchmarkDocument> Records, PaginationMetadata Metadata)> ListBenchmarksAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (records, total) = await _benchmarks.ListPageAsync(pageNumber, pageSize, cancellationToken);
        return (records, new PaginationMetadata(Math.Clamp(pageSize, 1, 200), Math.Max(1, pageNumber), total));
    }

    public Task<AutomationMetricsBenchmarkDocument?> GetBenchmarkAsync(string key, CancellationToken cancellationToken = default) =>
        _benchmarks.GetAsync(key, cancellationToken);

    public Task UpsertBenchmarkAsync(AutomationMetricsBenchmarkDocument document, CancellationToken cancellationToken = default) =>
        _benchmarks.UpsertAsync(document, cancellationToken);

    internal static MetricsRunListItem ToListItem(AutomationRunMetricsDocument document)
    {
        return new MetricsRunListItem
        {
            RunId = document.RunId,
            ScenarioId = document.ScenarioId,
            ScenarioName = document.ScenarioName,
            Outcome = document.Outcome,
            E2eDurationSeconds = document.E2eDurationSeconds,
            BenchmarkPass = document.Benchmark.Pass,
            StagesUnavailable = AreStagesUnavailable(document),
            FinishedAt = document.FinishedAt
        };
    }

    internal static MetricsRunDetailViewModel ToDetail(AutomationRunMetricsDocument document)
    {
        var item = ToListItem(document);
        return new MetricsRunDetailViewModel
        {
            RunId = item.RunId,
            ScenarioId = item.ScenarioId,
            ScenarioName = item.ScenarioName,
            Outcome = item.Outcome,
            E2eDurationSeconds = item.E2eDurationSeconds,
            BenchmarkPass = item.BenchmarkPass,
            StagesUnavailable = item.StagesUnavailable,
            FinishedAt = item.FinishedAt,
            BenchmarkKey = document.BenchmarkKey,
            PatientCount = document.PatientCount,
            PatientsPerMinute = document.Throughput.PatientsPerMinute,
            ResourcesPerSecond = document.Throughput.ResourcesPerSecond,
            ThetisGitSha = document.Thetis.GitSha,
            Seed = document.Thetis.Seed,
            Stages = ToStages(document),
            BenchmarkViolations = document.Benchmark.Violations,
            RegressionFlags = document.Regression.Flags,
            PreviousRunId = document.Regression.PreviousRunId,
            Validators = document.Validators.Select(v => new ValidatorOutcomeSnapshotView
            {
                Name = v.Name,
                Outcome = v.Outcome,
                IssueCount = v.IssueCount
            }).ToList()
        };
    }

    internal static MetricsRunDetailViewModel UnavailableFromRun(AutomationRunSummary run)
    {
        var finished = run.FinishedAt ?? DateTimeOffset.UtcNow;
        var started = run.StartedAt ?? run.CreatedAt;
        return new MetricsRunDetailViewModel
        {
            RunId = run.RunId,
            ScenarioName = run.RunName,
            Outcome = run.Status.ToString(),
            E2eDurationSeconds = Math.Max(0, (finished - started).TotalSeconds),
            BenchmarkPass = true,
            StagesUnavailable = true,
            FinishedAt = run.FinishedAt,
            PatientCount = run.PatientCount,
            Seed = run.Seed,
            Stages = EmptyUnavailableStages(),
            BenchmarkViolations = [],
            RegressionFlags = []
        };
    }

    private sealed class EmptyBenchmarkStore : IMetricsBenchmarkStore
    {
        public Task UpsertAsync(AutomationMetricsBenchmarkDocument document, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<AutomationMetricsBenchmarkDocument?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<AutomationMetricsBenchmarkDocument?>(null);

        public Task<(IReadOnlyList<AutomationMetricsBenchmarkDocument> Records, long TotalCount)> ListPageAsync(
            int pageNumber, int pageSize, CancellationToken cancellationToken = default) =>
            Task.FromResult(((IReadOnlyList<AutomationMetricsBenchmarkDocument>)[], 0L));
    }

    internal static bool AreStagesUnavailable(AutomationRunMetricsDocument document) =>
        document.Stages.Count == 0 || document.Stages.Values.All(s => s.Unavailable);

    internal static Dictionary<string, StageSnapshot> ToStages(AutomationRunMetricsDocument document)
    {
        var stages = EmptyUnavailableStages();
        foreach (var (name, snapshot) in document.Stages)
        {
            stages[name] = new StageSnapshot
            {
                Unavailable = snapshot.Unavailable,
                Count = (long)Math.Round(snapshot.Count),
                P50Ms = snapshot.Unavailable ? null : snapshot.P50Ms,
                P95Ms = snapshot.Unavailable ? null : snapshot.P95Ms,
                P99Ms = snapshot.Unavailable ? null : snapshot.P99Ms,
                ErrorCount = (long)Math.Round(snapshot.ErrorCount)
            };
        }

        return stages;
    }

    internal static Dictionary<string, StageSnapshot> EmptyUnavailableStages()
    {
        return RunMetricsSnapshotService.StageHistograms.ToDictionary(
            s => s.Stage,
            _ => new StageSnapshot { Unavailable = true },
            StringComparer.Ordinal);
    }
}
