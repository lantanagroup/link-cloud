using Automation.UI.Services.Persistence;
using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;

namespace Automation.UI.Services;

public interface IRunMetricsSnapshotService
{
    Task<AutomationRunMetricsDocument?> CaptureAsync(RunMetricsCaptureInput input, CancellationToken cancellationToken = default);
}

public sealed record RunMetricsCaptureInput(
    Guid RunId,
    Guid? ScenarioId,
    string ScenarioName,
    string? BenchmarkKey,
    bool IsMetricsRun,
    string Outcome,
    string FacilityId,
    string ReportId,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    int Seed,
    int PatientCount,
    int ResourcesPerPatientMin,
    int ResourcesPerPatientMax,
    int ManifestResourceCount,
    IReadOnlyList<PipelineSummarySnapshotBuilder.ValidatorResultSnapshot> Validators,
    int? TargetDurationSeconds = null,
    int? Concurrency = null,
    IReadOnlyList<string>? Measures = null,
    Guid? QueryPlanTemplateId = null,
    Guid? NormalizationSuiteId = null,
    long? GenerationDurationMs = null);

public sealed class RunMetricsSnapshotService : IRunMetricsSnapshotService
{
    internal static readonly StageQuery[] StageHistograms =
    [
        new("acquisition", "link_data_acq_query_duration_milliseconds", null, null),
        new("dispatch", "link_querydispatch_dispatch_duration_milliseconds", "link_querydispatch_patients_dispatched_count", "failure"),
        new("normalization", "link_normalization_duration_milliseconds", null, null),
        new("measureeval", "link_measureeval_eval_duration_milliseconds", "link_measureeval_eval_count", "failure"),
        new("validation", "link_validation_validate_duration_milliseconds", "link_validation_counter", "Failed"),
        new("submission", "link_submission_upload_duration_milliseconds", "link_submission_upload_count", "failure")
    ];

    internal readonly record struct StageQuery(
        string Stage,
        string HistogramBase,
        string? ErrorCounter,
        string? ErrorOutcome);

    private readonly IRunMetricsStore _store;
    private readonly IMetricsBenchmarkStore _benchmarks;
    private readonly IPrometheusHistogramClient _prometheus;
    private readonly IAutomationUiMetrics _metrics;
    private readonly TelemetrySettings _telemetry;
    private readonly TimeProvider _time;
    private readonly ILogger<RunMetricsSnapshotService> _logger;

    public RunMetricsSnapshotService(
        IRunMetricsStore store,
        IPrometheusHistogramClient prometheus,
        IAutomationUiMetrics metrics,
        IOptions<TelemetrySettings> telemetry,
        ILogger<RunMetricsSnapshotService> logger,
        TimeProvider? time = null,
        IMetricsBenchmarkStore? benchmarks = null)
    {
        _store = store;
        _prometheus = prometheus;
        _metrics = metrics;
        _telemetry = telemetry.Value;
        _logger = logger;
        _time = time ?? TimeProvider.System;
        _benchmarks = benchmarks ?? new NullBenchmarkStore();
    }

    public async Task<AutomationRunMetricsDocument?> CaptureAsync(RunMetricsCaptureInput input, CancellationToken cancellationToken = default)
    {
        if (!input.IsMetricsRun)
            return null;

        var e2eSeconds = Math.Max(0, (input.FinishedAt - input.StartedAt).TotalSeconds);
        var wait = ResolvePrometheusWait();
        var endpoint = _telemetry.PrometheusQueryEndpoint?.Trim();
        var stagesUnavailable = string.IsNullOrWhiteSpace(endpoint);
        var stages = StageHistograms.ToDictionary(
            s => s.Stage,
            _ => new StageLatencySnapshot { Unavailable = true },
            StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            try
            {
                await DelayAsync(wait, cancellationToken);
                var evaluationTime = _time.GetUtcNow();
                var windowSeconds = Math.Max(60, (int)Math.Ceiling(e2eSeconds + wait.TotalSeconds + 5));
                var anyStage = false;
                foreach (var stageQuery in StageHistograms)
                {
                    var snapshot = await QueryStageAsync(stageQuery, input.FacilityId, windowSeconds, evaluationTime, cancellationToken);
                    stages[stageQuery.Stage] = snapshot;
                    if (!snapshot.Unavailable)
                        anyStage = true;
                }

                stagesUnavailable = !anyStage;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Prometheus enrichment failed for metrics run {RunId}; persisting wall-clock snapshot.", input.RunId);
                stagesUnavailable = true;
            }
        }

        if (stagesUnavailable)
            _metrics.IncrementSnapshotMissing();

        var document = new AutomationRunMetricsDocument
        {
            RunId = input.RunId,
            ScenarioId = input.ScenarioId,
            ScenarioName = input.ScenarioName,
            BenchmarkKey = input.BenchmarkKey,
            FacilityId = input.FacilityId,
            ReportId = input.ReportId,
            StartedAt = input.StartedAt,
            FinishedAt = input.FinishedAt,
            CreatedAt = _time.GetUtcNow(),
            Outcome = input.Outcome,
            PatientCount = input.PatientCount,
            ResourcesPerPatientMin = input.ResourcesPerPatientMin,
            ResourcesPerPatientMax = input.ResourcesPerPatientMax,
            Thetis = new ThetisRevisionSnapshot
            {
                Generator = "thetis",
                Source = "sibling-project-ref",
                GitSha = ThetisRevision.TryGetGitSha(),
                AssemblyInformationalVersion = ThetisRevision.TryGetAssemblyInformationalVersion(),
                Seed = input.Seed,
                DurationMs = input.GenerationDurationMs ?? 0
            },
            PrometheusWaitMs = (long)wait.TotalMilliseconds,
            Stages = stages,
            Throughput = new ThroughputSnapshot
            {
                PatientsPerMinute = e2eSeconds > 0 ? input.PatientCount / (e2eSeconds / 60.0) : 0,
                ResourcesPerSecond = e2eSeconds > 0 ? input.ManifestResourceCount / e2eSeconds : 0
            },
            E2eDurationSeconds = e2eSeconds,
            SetupSummary = MetricsScenarioFingerprint.Describe(
                input.PatientCount,
                input.Seed,
                input.ResourcesPerPatientMin,
                input.ResourcesPerPatientMax,
                input.Concurrency),
            ScenarioFingerprint = MetricsScenarioFingerprint.Compute(
                input.PatientCount,
                input.Seed,
                input.ResourcesPerPatientMin,
                input.ResourcesPerPatientMax,
                input.Concurrency,
                input.BenchmarkKey,
                input.Measures,
                ThetisRevision.TryGetGitSha(),
                input.QueryPlanTemplateId,
                input.NormalizationSuiteId),
            Validators = input.Validators.Select(v => new ValidatorOutcomeSnapshot
            {
                Name = v.Name,
                Outcome = v.Outcome,
                IssueCount = v.IssueCount
            }).ToList()
        };

        AutomationMetricsBenchmarkDocument? benchmark = null;
        if (!string.IsNullOrWhiteSpace(input.BenchmarkKey))
            benchmark = await _benchmarks.GetAsync(input.BenchmarkKey, cancellationToken);

        AutomationRunMetricsDocument? previous = null;
        if (input.ScenarioId is Guid scenarioId && scenarioId != Guid.Empty)
        {
            var previousAny = await _store.GetPreviousAsync(
                scenarioId,
                input.FinishedAt,
                input.RunId,
                cancellationToken);
            document.ScenarioVersion = MetricsScenarioFingerprint.NextVersion(
                previousAny?.ScenarioFingerprint,
                previousAny?.ScenarioVersion ?? 1,
                document.ScenarioFingerprint ?? "");

            previous = await _store.GetPreviousSucceededSameFingerprintAsync(
                scenarioId,
                document.ScenarioFingerprint ?? "",
                input.FinishedAt,
                input.RunId,
                cancellationToken) ?? await _store.GetPreviousSucceededAsync(
                scenarioId,
                input.FinishedAt,
                input.RunId,
                cancellationToken);
        }

        var evaluation = MetricsBenchmarkEvaluator.Evaluate(
            document,
            benchmark,
            input.TargetDurationSeconds,
            previous);
        document.Benchmark = new BenchmarkResultSnapshot
        {
            Key = evaluation.Key,
            Pass = evaluation.Pass,
            Violations = evaluation.Violations.ToList()
        };
        document.Regression = new RegressionResultSnapshot
        {
            PreviousRunId = evaluation.PreviousRunId,
            Flags = evaluation.RegressionFlags.ToList()
        };

        await _store.UpsertAsync(document, cancellationToken);
        _logger.LogInformation(
            "Persisted automation_run_metrics for run {RunId} (stagesUnavailable={Unavailable}, benchmarkPass={Pass})",
            input.RunId,
            stagesUnavailable,
            evaluation.Pass);
        return document;
    }

    private sealed class NullBenchmarkStore : IMetricsBenchmarkStore
    {
        public Task UpsertAsync(AutomationMetricsBenchmarkDocument document, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<AutomationMetricsBenchmarkDocument?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<AutomationMetricsBenchmarkDocument?>(null);

        public Task<(IReadOnlyList<AutomationMetricsBenchmarkDocument> Records, long TotalCount)> ListPageAsync(
            int pageNumber, int pageSize, CancellationToken cancellationToken = default) =>
            Task.FromResult(((IReadOnlyList<AutomationMetricsBenchmarkDocument>)[], 0L));
    }

    private async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        if (delay <= TimeSpan.Zero)
            return;

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var timer = _time.CreateTimer(_ => completion.TrySetResult(), null, delay, Timeout.InfiniteTimeSpan);
        using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        await completion.Task;
    }

    public static TimeSpan ResolvePrometheusWait()
    {
        var exportMs = 60_000;
        var env = Environment.GetEnvironmentVariable("OTEL_METRIC_EXPORT_INTERVAL");
        if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env, out var parsed) && parsed > 0)
            exportMs = parsed;

        const int scrapeMs = 10_000;
        return TimeSpan.FromMilliseconds(exportMs + scrapeMs + 1_000);
    }

    private async Task<StageLatencySnapshot> QueryStageAsync(
        StageQuery stage,
        string facilityId,
        int windowSeconds,
        DateTimeOffset evaluationTime,
        CancellationToken cancellationToken)
    {
        var facility = EscapePromLabel(facilityId);
        var selector = $"{stage.HistogramBase}_bucket{{facility_id=\"{facility}\"}}[{windowSeconds}s]";
        var countSelector = $"sum(increase({stage.HistogramBase}_count{{facility_id=\"{facility}\"}}[{windowSeconds}s]))";

        var count = await _prometheus.QueryScalarAsync(countSelector, evaluationTime, cancellationToken);
        if (count is null or <= 0)
            return new StageLatencySnapshot { Unavailable = true };

        var p50 = await _prometheus.QueryScalarAsync(
            $"histogram_quantile(0.50, sum by (le) (increase({selector})))", evaluationTime, cancellationToken);
        var p95 = await _prometheus.QueryScalarAsync(
            $"histogram_quantile(0.95, sum by (le) (increase({selector})))", evaluationTime, cancellationToken);
        var p99 = await _prometheus.QueryScalarAsync(
            $"histogram_quantile(0.99, sum by (le) (increase({selector})))", evaluationTime, cancellationToken);

        double errorCount = 0;
        if (!string.IsNullOrWhiteSpace(stage.ErrorCounter) && !string.IsNullOrWhiteSpace(stage.ErrorOutcome))
        {
            var errorSelector =
                $"sum(increase({stage.ErrorCounter}{{facility_id=\"{facility}\",outcome=\"{EscapePromLabel(stage.ErrorOutcome)}\"}}[{windowSeconds}s]))";
            errorCount = await _prometheus.QueryScalarAsync(errorSelector, evaluationTime, cancellationToken) ?? 0;
        }

        return new StageLatencySnapshot
        {
            Unavailable = p50 is null && p95 is null && p99 is null,
            Count = count.Value,
            P50Ms = p50 ?? 0,
            P95Ms = p95 ?? 0,
            P99Ms = p99 ?? 0,
            ErrorCount = Math.Max(0, errorCount)
        };
    }

    private static string EscapePromLabel(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
