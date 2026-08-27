using Automation.UI.Models;
using Automation.UI.Models.Metrics;
using Automation.UI.Services.Persistence;
using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace Automation.UI.Services;

public sealed class MetricsRunPresenter
{
    public const int WindowDays = 14;
    public const int HistoryDays = 90;

    private static readonly (string Key, string Name, string Hint)[] ServiceOrder =
    [
        ("acquisition", "Data Acquisition", "Pulling FHIR from the server"),
        ("dispatch", "Query Dispatch", "Telling Data Acquisition who to pull"),
        ("normalization", "Normalization", "Cleaning and reshaping FHIR"),
        ("measureeval", "Measure Evaluation", "Running the measure"),
        ("validation", "Validation", "Checking the measure report"),
        ("submission", "Submission", "Uploading the report package")
    ];

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
        var (records, total) = await _store.ListPageAsync(pageNumber, pageSize, cancellationToken: cancellationToken);
        return (records.Select(ToListItem).ToList(), new PaginationMetadata(pageSize, pageNumber, total));
    }

    public async Task<MetricsDashboardViewModel> GetDashboardAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (records, metadata) = await ListAsync(pageNumber, pageSize, cancellationToken);
        var since = DateTimeOffset.UtcNow.AddDays(-HistoryDays);
        var recent = await _store.ListSinceAsync(since, cancellationToken);
        var last14 = recent.Where(d => d.FinishedAt >= DateTimeOffset.UtcNow.AddDays(-WindowDays)).ToList();
        var lastDoc = last14.FirstOrDefault() ?? recent.FirstOrDefault();

        var scenarios = (await _scenarioStore.GetAllAsync(cancellationToken))
            .Where(s => s.IsMetricsRun)
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var names = scenarios.ToDictionary(s => s.Id, s => s.Name);

        return new MetricsDashboardViewModel
        {
            LastRunE2eSeconds = lastDoc?.E2eDurationSeconds ?? records.FirstOrDefault()?.E2eDurationSeconds ?? 0,
            LastRunPatientsPerMinute = lastDoc?.Throughput.PatientsPerMinute,
            LastRunStagesUnavailable = lastDoc == null
                ? records.FirstOrDefault()?.StagesUnavailable ?? true
                : AreStagesUnavailable(lastDoc),
            RegressionFlagCount = last14.Sum(d => d.Regression.Flags.Count),
            FleetPatientsPerMinute = Median(last14
                .Where(d => d.Outcome == "Succeeded" && d.Throughput.PatientsPerMinute > 0)
                .Select(d => d.Throughput.PatientsPerMinute)),
            ScenarioCount = scenarios.Count,
            RecentRunCount = last14.Count,
            Services = BuildServiceStrip(last14),
            ScenarioCards = BuildScenarioCards(recent, names),
            Runs = records,
            Metadata = metadata,
            MetricsScenarios = scenarios,
            DurationTrend = last14
                .OrderBy(d => d.FinishedAt)
                .Select(ToPoint)
                .ToList()
        };
    }

    public async Task<MetricsScenarioHistoryViewModel?> GetScenarioHistoryAsync(
        Guid scenarioId,
        CancellationToken cancellationToken = default)
    {
        var docs = await _store.ListByScenarioAsync(scenarioId, cancellationToken);
        var scenario = await _scenarioStore.GetByIdAsync(scenarioId, cancellationToken);
        if (docs.Count == 0 && scenario == null)
            return null;

        var latest = docs.LastOrDefault();
        var versions = docs.Select(d => d.ScenarioVersion).Where(v => v > 0).Distinct().Count();
        return new MetricsScenarioHistoryViewModel
        {
            ScenarioId = scenarioId,
            Name = scenario?.Name ?? latest?.ScenarioName ?? "Scenario",
            SetupSummary = latest?.SetupSummary,
            CurrentVersion = latest?.ScenarioVersion ?? 1,
            HasVersionChange = versions > 1,
            DurationTrend = docs.Select(ToPoint).ToList(),
            Runs = docs.OrderByDescending(d => d.FinishedAt).Select(ToListItem).ToList()
        };
    }

    public async Task<MetricsCompareViewModel?> GetCompareAsync(
        Guid leftId,
        Guid rightId,
        CancellationToken cancellationToken = default)
    {
        var left = await GetDetailAsync(leftId, cancellationToken);
        var right = await GetDetailAsync(rightId, cancellationToken);
        if (left == null || right == null)
            return null;

        return new MetricsCompareViewModel { Left = left, Right = right };
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
            FinishedAt = document.FinishedAt,
            ScenarioVersion = Math.Max(1, document.ScenarioVersion),
            SetupSummary = document.SetupSummary,
            PatientsPerMinute = document.Throughput.PatientsPerMinute > 0 ? document.Throughput.PatientsPerMinute : null
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
            ScenarioVersion = item.ScenarioVersion,
            SetupSummary = item.SetupSummary,
            PatientsPerMinute = item.PatientsPerMinute,
            BenchmarkKey = document.BenchmarkKey,
            PatientCount = document.PatientCount,
            ResourcesPerSecond = document.Throughput.ResourcesPerSecond,
            ThetisGitSha = document.Thetis.GitSha,
            Seed = document.Thetis.Seed,
            GenerationDurationMs = document.Thetis.DurationMs,
            ScenarioFingerprint = document.ScenarioFingerprint,
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

    private static MetricsDurationPoint ToPoint(AutomationRunMetricsDocument d) => new()
    {
        RunId = d.RunId,
        FinishedAt = d.FinishedAt,
        E2eDurationSeconds = d.E2eDurationSeconds,
        PatientsPerMinute = d.Throughput.PatientsPerMinute > 0 ? d.Throughput.PatientsPerMinute : null,
        ScenarioVersion = Math.Max(1, d.ScenarioVersion)
    };

    private static IReadOnlyList<MetricsServiceHealthItem> BuildServiceStrip(
        IReadOnlyList<AutomationRunMetricsDocument> recent)
    {
        return ServiceOrder.Select(s =>
        {
            var values = recent
                .Select(d => d.Stages.TryGetValue(s.Key, out var stage) ? stage : null)
                .Where(stage => stage is { Unavailable: false, P95Ms: > 0 })
                .Select(stage => stage!.P95Ms)
                .ToList();
            return new MetricsServiceHealthItem
            {
                Key = s.Key,
                Name = s.Name,
                Hint = s.Hint,
                SlowMs = Median(values),
                Unavailable = values.Count == 0
            };
        }).ToList();
    }

    private static IReadOnlyList<MetricsScenarioCardViewModel> BuildScenarioCards(
        IReadOnlyList<AutomationRunMetricsDocument> recent,
        IReadOnlyDictionary<Guid, string> names)
    {
        return recent
            .Where(d => d.ScenarioId is Guid id && id != Guid.Empty)
            .GroupBy(d => d.ScenarioId!.Value)
            .Select(g =>
            {
                var ordered = g.OrderBy(d => d.FinishedAt).ToList();
                var last = ordered[^1];
                var versions = ordered.Select(d => d.ScenarioVersion).Where(v => v > 0).Distinct().Count();
                return new MetricsScenarioCardViewModel
                {
                    ScenarioId = g.Key,
                    Name = names.TryGetValue(g.Key, out var name) ? name : last.ScenarioName,
                    SetupSummary = last.SetupSummary,
                    RunCount = ordered.Count,
                    ScenarioVersion = Math.Max(1, last.ScenarioVersion),
                    VersionChanged = versions > 1,
                    Outcome = last.Outcome,
                    LastE2eSeconds = last.E2eDurationSeconds,
                    LastPatientsPerMinute = last.Throughput.PatientsPerMinute > 0 ? last.Throughput.PatientsPerMinute : null,
                    LastStagesUnavailable = AreStagesUnavailable(last),
                    GotSlower = last.Regression.Flags.Count > 0,
                    LastFinishedAt = last.FinishedAt,
                    LastRunId = last.RunId,
                    Sparkline = ordered.TakeLast(12).Select(d => d.E2eDurationSeconds).ToList()
                };
            })
            .OrderByDescending(c => c.LastFinishedAt)
            .ToList();
    }

    private static double? Median(IEnumerable<double> values)
    {
        var list = values.OrderBy(v => v).ToList();
        if (list.Count == 0)
            return null;
        var mid = list.Count / 2;
        return list.Count % 2 == 1 ? list[mid] : (list[mid - 1] + list[mid]) / 2.0;
    }
}
