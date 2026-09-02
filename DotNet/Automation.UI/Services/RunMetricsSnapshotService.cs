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
    long? GenerationDurationMs = null,
    DateTime? ReportCreatedAt = null,
    DateTime? SubmittedAt = null);

public sealed class RunMetricsSnapshotService : IRunMetricsSnapshotService
{
    // Query Dispatch meters only fire on the scheduled discharge job, which adhoc
    // metrics runs never execute. Leave that service's OTEL instruments in place
    // for Grafana; do not show an always-empty step here.
    internal static readonly StageQuery[] StageHistograms =
    [
        new("acquisition", "link_data_acq_query_duration_milliseconds", null, null),
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

        var window = ResolvePipelineWindow(input.StartedAt, input.FinishedAt, input.ReportCreatedAt, input.SubmittedAt);
        var e2eSeconds = Math.Max(0, (window.FinishedAt - window.StartedAt).TotalSeconds);
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
                if (!await _prometheus.IsReachableAsync(cancellationToken))
                {
                    _logger.LogWarning(
                        "Prometheus is not reachable at {Endpoint} for metrics run {RunId} facility {FacilityId}; persisting wall-clock snapshot without step timings.",
                        endpoint,
                        input.RunId,
                        input.FacilityId);
                    stagesUnavailable = true;
                }
                else
                {
                    await DelayAsync(wait, cancellationToken);
                    var evaluationTime = _time.GetUtcNow();
                    var anyStage = false;
                    foreach (var stageQuery in StageHistograms)
                    {
                        var snapshot = await QueryStageAsync(stageQuery, input.FacilityId, evaluationTime, cancellationToken);
                        stages[stageQuery.Stage] = snapshot;
                        if (!snapshot.Unavailable)
                            anyStage = true;
                    }

                    stagesUnavailable = !anyStage;
                    if (stagesUnavailable)
                    {
                        _logger.LogWarning(
                            "Prometheus at {Endpoint} had no step timings for metrics run {RunId} facility {FacilityId}.",
                            endpoint,
                            input.RunId,
                            input.FacilityId);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Prometheus enrichment failed for metrics run {RunId} at {Endpoint} facility {FacilityId}; persisting wall-clock snapshot.",
                    input.RunId,
                    endpoint,
                    input.FacilityId);
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
            StartedAt = window.StartedAt,
            FinishedAt = window.FinishedAt,
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
                window.FinishedAt,
                input.RunId,
                cancellationToken);
            document.ScenarioVersion = MetricsScenarioFingerprint.NextVersion(
                previousAny?.ScenarioFingerprint,
                previousAny?.ScenarioVersion ?? 1,
                document.ScenarioFingerprint ?? "");

            previous = await _store.GetPreviousSucceededSameFingerprintAsync(
                scenarioId,
                document.ScenarioFingerprint ?? "",
                window.FinishedAt,
                input.RunId,
                cancellationToken) ?? await _store.GetPreviousSucceededAsync(
                scenarioId,
                window.FinishedAt,
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

    /// <summary>
    /// Metrics total time is report created → ABS submit, not execute-start through
    /// cleanup or the Prometheus wait.
    /// </summary>
    public static (DateTimeOffset StartedAt, DateTimeOffset FinishedAt) ResolvePipelineWindow(
        DateTimeOffset runStartedAt,
        DateTimeOffset runFinishedAt,
        DateTime? reportCreatedAt,
        DateTime? submittedAt)
    {
        if (reportCreatedAt is DateTime created && submittedAt is DateTime submitted)
        {
            var start = AsUtc(created);
            var end = AsUtc(submitted);
            if (end > start)
                return (start, end);
        }

        return (runStartedAt, runFinishedAt);
    }

    private static DateTimeOffset AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified
            ? new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc))
            : new DateTimeOffset(value.ToUniversalTime());

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
        DateTimeOffset evaluationTime,
        CancellationToken cancellationToken)
    {
        var facility = EscapePromLabel(facilityId);
        var bucketSelector = $"{stage.HistogramBase}_bucket{{facility_id=\"{facility}\"}}";
        var countSelector = $"sum({stage.HistogramBase}_count{{facility_id=\"{facility}\"}})";

        var count = await _prometheus.QueryScalarAsync(countSelector, evaluationTime, cancellationToken);
        if (count is null or <= 0)
            return new StageLatencySnapshot { Unavailable = true };

        var p50 = await _prometheus.QueryScalarAsync(
            $"histogram_quantile(0.50, sum by (le) ({bucketSelector}))", evaluationTime, cancellationToken);
        var p95 = await _prometheus.QueryScalarAsync(
            $"histogram_quantile(0.95, sum by (le) ({bucketSelector}))", evaluationTime, cancellationToken);
        var p99 = await _prometheus.QueryScalarAsync(
            $"histogram_quantile(0.99, sum by (le) ({bucketSelector}))", evaluationTime, cancellationToken);

        double errorCount = 0;
        if (!string.IsNullOrWhiteSpace(stage.ErrorCounter) && !string.IsNullOrWhiteSpace(stage.ErrorOutcome))
        {
            var errorSelector =
                $"sum({stage.ErrorCounter}{{facility_id=\"{facility}\",outcome=\"{EscapePromLabel(stage.ErrorOutcome)}\"}})";
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
