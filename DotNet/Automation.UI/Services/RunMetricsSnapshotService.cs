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

    internal enum ProcessRuntimeKind
    {
        DotNet,
        Jvm
    }

    internal readonly record struct ProcessUtilizationQuery(
        string Key,
        string Name,
        string Hint,
        string ExportedJob,
        ProcessRuntimeKind Runtime);

    // Process RSS/CPU is not facility-scoped. The lookback is the pipeline window
    // (report created → ABS submit), which is accurate when one metrics run is in flight.
    internal static readonly ProcessUtilizationQuery[] ProcessUtilizationQueries =
    [
        new("acquisition", "Data Acquisition", "API process", "DataAcquisition", ProcessRuntimeKind.DotNet),
        new("acquisition-worker", "Data Acquisition worker", "FHIR query worker", "DataAcquisitionWorker", ProcessRuntimeKind.DotNet),
        new("normalization", "Normalization", "Cleaning and reshaping FHIR", "Normalization", ProcessRuntimeKind.DotNet),
        new("report", "Report", "Report store and schedule", "Report", ProcessRuntimeKind.DotNet),
        new("measureeval", "Measure Evaluation", "Running the measure", "measureeval", ProcessRuntimeKind.Jvm),
        new("validation", "Validation", "Checking the measure report", "ValidationService", ProcessRuntimeKind.Jvm),
        new("submission", "Submission", "Uploading the report package", "Submission", ProcessRuntimeKind.DotNet)
    ];

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
        var utilization = ProcessUtilizationQueries.ToDictionary(
            s => s.Key,
            _ => new ProcessUtilizationSnapshot { Unavailable = true },
            StringComparer.Ordinal);
        var apiLatency = ProcessUtilizationQueries.ToDictionary(
            s => s.Key,
            _ => new ApiLatencySnapshot { Unavailable = true },
            StringComparer.Ordinal);
        List<ApiRouteLatencySnapshot> slowestRoutes = [];

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

                    var lookbackSeconds = ResolveUtilizationLookbackSeconds(e2eSeconds);
                    foreach (var processQuery in ProcessUtilizationQueries)
                    {
                        utilization[processQuery.Key] = await QueryProcessUtilizationAsync(
                            processQuery,
                            lookbackSeconds,
                            window.FinishedAt,
                            cancellationToken);
                        apiLatency[processQuery.Key] = await QueryApiLatencyAsync(
                            processQuery,
                            lookbackSeconds,
                            window.FinishedAt,
                            cancellationToken);
                    }

                    slowestRoutes = await QuerySlowestApiRoutesAsync(
                        lookbackSeconds,
                        window.FinishedAt,
                        cancellationToken);
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
            ProcessUtilization = utilization,
            ApiLatency = apiLatency,
            SlowestApiRoutes = slowestRoutes,
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

    internal static int ResolveUtilizationLookbackSeconds(double e2eSeconds) =>
        Math.Max(30, (int)Math.Ceiling(Math.Max(0, e2eSeconds)));

    internal static string DotNetPeakMemoryQuery(string exportedJob, int windowSeconds) =>
        $"max(max_over_time(process_memory_usage_bytes{{exported_job=\"{EscapePromLabel(exportedJob)}\"}}[{windowSeconds}s]))";

    internal static string DotNetAvgMemoryQuery(string exportedJob, int windowSeconds) =>
        $"avg(avg_over_time(process_memory_usage_bytes{{exported_job=\"{EscapePromLabel(exportedJob)}\"}}[{windowSeconds}s]))";

    internal static string DotNetAvgCpuCoresQuery(string exportedJob, int windowSeconds) =>
        $"sum(increase(process_cpu_time_seconds_total{{exported_job=\"{EscapePromLabel(exportedJob)}\"}}[{windowSeconds}s])) / {windowSeconds}";

    internal static string DotNetPeakCpuCoresQuery(string exportedJob, int windowSeconds) =>
        $"max_over_time(sum(rate(process_cpu_time_seconds_total{{exported_job=\"{EscapePromLabel(exportedJob)}\"}}[30s]))[{windowSeconds}s:10s])";

    internal static string DotNetCpuCountQuery(string exportedJob) =>
        $"avg(process_cpu_count{{exported_job=\"{EscapePromLabel(exportedJob)}\"}})";

    internal static string JvmPeakHeapQuery(string exportedJob, int windowSeconds) =>
        $"sum(max_over_time(jvm_memory_used_bytes{{exported_job=\"{EscapePromLabel(exportedJob)}\",jvm_memory_type=\"heap\"}}[{windowSeconds}s]))";

    internal static string JvmAvgHeapQuery(string exportedJob, int windowSeconds) =>
        $"sum(avg_over_time(jvm_memory_used_bytes{{exported_job=\"{EscapePromLabel(exportedJob)}\",jvm_memory_type=\"heap\"}}[{windowSeconds}s]))";

    internal static string JvmAvgCpuRatioQuery(string exportedJob, int windowSeconds) =>
        $"avg_over_time(jvm_cpu_recent_utilization_ratio{{exported_job=\"{EscapePromLabel(exportedJob)}\"}}[{windowSeconds}s])";

    internal static string JvmPeakCpuRatioQuery(string exportedJob, int windowSeconds) =>
        $"max_over_time(jvm_cpu_recent_utilization_ratio{{exported_job=\"{EscapePromLabel(exportedJob)}\"}}[{windowSeconds}s])";

    internal static string JvmCpuCountQuery(string exportedJob) =>
        $"avg(jvm_cpu_count{{exported_job=\"{EscapePromLabel(exportedJob)}\"}})";

    internal const string HttpRouteExclude = "/health|/api/health|/hubs/.*";

    internal static string HttpCountQuery(string exportedJob, int windowSeconds) =>
        $"sum(increase(http_server_request_duration_seconds_count{{exported_job=\"{EscapePromLabel(exportedJob)}\",http_route!~\"{HttpRouteExclude}\"}}[{windowSeconds}s]))";

    internal static string HttpErrorCountQuery(string exportedJob, int windowSeconds) =>
        $"sum(increase(http_server_request_duration_seconds_count{{exported_job=\"{EscapePromLabel(exportedJob)}\",http_response_status_code=~\"5..\"}}[{windowSeconds}s]))";

    internal static string HttpQuantileQuery(string exportedJob, int windowSeconds, string quantile) =>
        $"histogram_quantile({quantile}, sum by (le) (increase(http_server_request_duration_seconds_bucket{{exported_job=\"{EscapePromLabel(exportedJob)}\",http_route!~\"{HttpRouteExclude}\"}}[{windowSeconds}s])))";

    internal static string HttpSlowestRoutesQuery(int windowSeconds) =>
        $"histogram_quantile(0.95, sum by (le, exported_job, http_route, http_request_method) (increase(http_server_request_duration_seconds_bucket{{http_route!~\"{HttpRouteExclude}\"}}[{windowSeconds}s])))";

    internal static string HttpRouteCountQuery(int windowSeconds) =>
        $"sum by (exported_job, http_route, http_request_method) (increase(http_server_request_duration_seconds_count{{http_route!~\"{HttpRouteExclude}\"}}[{windowSeconds}s]))";

    private async Task<ApiLatencySnapshot> QueryApiLatencyAsync(
        ProcessUtilizationQuery process,
        int windowSeconds,
        DateTimeOffset evaluationTime,
        CancellationToken cancellationToken)
    {
        var count = await _prometheus.QueryScalarAsync(
            HttpCountQuery(process.ExportedJob, windowSeconds), evaluationTime, cancellationToken);
        if (count is null or <= 0)
            return new ApiLatencySnapshot { Unavailable = true };

        var p50Sec = await _prometheus.QueryScalarAsync(
            HttpQuantileQuery(process.ExportedJob, windowSeconds, "0.50"), evaluationTime, cancellationToken);
        var p95Sec = await _prometheus.QueryScalarAsync(
            HttpQuantileQuery(process.ExportedJob, windowSeconds, "0.95"), evaluationTime, cancellationToken);
        var p99Sec = await _prometheus.QueryScalarAsync(
            HttpQuantileQuery(process.ExportedJob, windowSeconds, "0.99"), evaluationTime, cancellationToken);
        var errors = await _prometheus.QueryScalarAsync(
            HttpErrorCountQuery(process.ExportedJob, windowSeconds), evaluationTime, cancellationToken) ?? 0;

        return new ApiLatencySnapshot
        {
            Unavailable = p50Sec is null && p95Sec is null && p99Sec is null,
            Count = count.Value,
            P50Ms = SecondsToMs(p50Sec),
            P95Ms = SecondsToMs(p95Sec),
            P99Ms = SecondsToMs(p99Sec),
            ErrorCount = Math.Max(0, errors)
        };
    }

    private async Task<List<ApiRouteLatencySnapshot>> QuerySlowestApiRoutesAsync(
        int windowSeconds,
        DateTimeOffset evaluationTime,
        CancellationToken cancellationToken)
    {
        var p95 = await _prometheus.QueryVectorAsync(
            HttpSlowestRoutesQuery(windowSeconds), evaluationTime, cancellationToken);
        var counts = await _prometheus.QueryVectorAsync(
            HttpRouteCountQuery(windowSeconds), evaluationTime, cancellationToken);
        var countByKey = counts.ToDictionary(RouteKey, s => s.Value, StringComparer.Ordinal);

        return p95
            .Where(s => s.Value > 0 && !string.IsNullOrWhiteSpace(ReadLabel(s, "http_route")))
            .Select(s => new ApiRouteLatencySnapshot
            {
                Service = DisplayNameForJob(s.ExportedJob),
                Method = ReadLabel(s, "http_request_method"),
                Route = ReadLabel(s, "http_route"),
                P95Ms = SecondsToMs(s.Value),
                Count = countByKey.TryGetValue(RouteKey(s), out var n) ? n : 0
            })
            .OrderByDescending(r => r.P95Ms)
            .Take(8)
            .ToList();
    }

    private static string RouteKey(PromSample sample) =>
        $"{sample.ExportedJob}\0{ReadLabel(sample, "http_request_method")}\0{ReadLabel(sample, "http_route")}";

    private static string ReadLabel(PromSample sample, string name) =>
        sample.Labels is not null && sample.Labels.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : "";

    private static string DisplayNameForJob(string exportedJob)
    {
        var match = ProcessUtilizationQueries.FirstOrDefault(q =>
            string.Equals(q.ExportedJob, exportedJob, StringComparison.Ordinal));
        return string.IsNullOrWhiteSpace(match.Name)
            ? LiveProcessUtilizationService.DisplayName(exportedJob)
            : match.Name;
    }

    private static double SecondsToMs(double? seconds) =>
        seconds is null ? 0 : Math.Max(0, seconds.Value * 1000.0);

    private async Task<ProcessUtilizationSnapshot> QueryProcessUtilizationAsync(
        ProcessUtilizationQuery process,
        int windowSeconds,
        DateTimeOffset evaluationTime,
        CancellationToken cancellationToken)
    {
        if (process.Runtime == ProcessRuntimeKind.Jvm)
            return await QueryJvmUtilizationAsync(process.ExportedJob, windowSeconds, evaluationTime, cancellationToken);

        var peakMemory = await _prometheus.QueryScalarAsync(
            DotNetPeakMemoryQuery(process.ExportedJob, windowSeconds), evaluationTime, cancellationToken);
        var avgMemory = await _prometheus.QueryScalarAsync(
            DotNetAvgMemoryQuery(process.ExportedJob, windowSeconds), evaluationTime, cancellationToken);
        var avgCpu = await _prometheus.QueryScalarAsync(
            DotNetAvgCpuCoresQuery(process.ExportedJob, windowSeconds), evaluationTime, cancellationToken);
        var peakCpu = await _prometheus.QueryScalarAsync(
            DotNetPeakCpuCoresQuery(process.ExportedJob, windowSeconds), evaluationTime, cancellationToken);
        var cpuCount = await _prometheus.QueryScalarAsync(
            DotNetCpuCountQuery(process.ExportedJob), evaluationTime, cancellationToken);

        return ToUtilizationSnapshot(avgCpu, peakCpu, avgMemory, peakMemory, cpuCount);
    }

    private async Task<ProcessUtilizationSnapshot> QueryJvmUtilizationAsync(
        string exportedJob,
        int windowSeconds,
        DateTimeOffset evaluationTime,
        CancellationToken cancellationToken)
    {
        var peakMemory = await _prometheus.QueryScalarAsync(
            JvmPeakHeapQuery(exportedJob, windowSeconds), evaluationTime, cancellationToken);
        var avgMemory = await _prometheus.QueryScalarAsync(
            JvmAvgHeapQuery(exportedJob, windowSeconds), evaluationTime, cancellationToken);
        var cpuCount = await _prometheus.QueryScalarAsync(
            JvmCpuCountQuery(exportedJob), evaluationTime, cancellationToken) ?? 0;
        var avgRatio = await _prometheus.QueryScalarAsync(
            JvmAvgCpuRatioQuery(exportedJob, windowSeconds), evaluationTime, cancellationToken);
        var peakRatio = await _prometheus.QueryScalarAsync(
            JvmPeakCpuRatioQuery(exportedJob, windowSeconds), evaluationTime, cancellationToken);
        var avgCpu = avgRatio is null || cpuCount <= 0 ? (double?)null : avgRatio.Value * cpuCount;
        var peakCpu = peakRatio is null || cpuCount <= 0 ? (double?)null : peakRatio.Value * cpuCount;

        return ToUtilizationSnapshot(avgCpu, peakCpu, avgMemory, peakMemory, cpuCount);
    }

    private static ProcessUtilizationSnapshot ToUtilizationSnapshot(
        double? avgCpu,
        double? peakCpu,
        double? avgMemory,
        double? peakMemory,
        double? cpuCount)
    {
        if ((avgMemory is null or <= 0) && (peakMemory is null or <= 0)
            && (avgCpu is null or <= 0) && (peakCpu is null or <= 0))
            return new ProcessUtilizationSnapshot { Unavailable = true };

        var avgCores = Math.Max(0, avgCpu ?? 0);
        var peakCores = Math.Max(0, peakCpu ?? avgCpu ?? 0);
        return new ProcessUtilizationSnapshot
        {
            Unavailable = false,
            AvgCpuCores = avgCores,
            PeakCpuCores = peakCores,
            AvgCpuPercent = LiveProcessUtilizationService.ToTaskManagerPercent(avgCores, cpuCount ?? 0),
            PeakCpuPercent = LiveProcessUtilizationService.ToTaskManagerPercent(peakCores, cpuCount ?? 0),
            AvgMemoryBytes = Math.Max(0, avgMemory ?? 0),
            PeakMemoryBytes = Math.Max(0, peakMemory ?? avgMemory ?? 0)
        };
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
