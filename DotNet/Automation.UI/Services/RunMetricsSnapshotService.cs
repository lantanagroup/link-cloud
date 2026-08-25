using Automation.UI.Services.Persistence;
using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;

namespace Automation.UI.Services;

public interface IRunMetricsSnapshotService
{
    Task CaptureAsync(RunMetricsCaptureInput input, CancellationToken cancellationToken = default);
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
    IReadOnlyList<PipelineSummarySnapshotBuilder.ValidatorResultSnapshot> Validators);

public sealed class RunMetricsSnapshotService : IRunMetricsSnapshotService
{
    internal static readonly (string Stage, string HistogramBase)[] StageHistograms =
    [
        ("acquisition", "link_data_acq_query_duration_milliseconds"),
        ("dispatch", "link_querydispatch_dispatch_duration_milliseconds"),
        ("normalization", "link_normalization_duration_milliseconds"),
        ("measureeval", "link_measureeval_eval_duration_milliseconds"),
        ("validation", "link_validation_validate_duration_milliseconds"),
        ("submission", "link_submission_upload_duration_milliseconds")
    ];

    private readonly IRunMetricsStore _store;
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
        TimeProvider? time = null)
    {
        _store = store;
        _prometheus = prometheus;
        _metrics = metrics;
        _telemetry = telemetry.Value;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    public async Task CaptureAsync(RunMetricsCaptureInput input, CancellationToken cancellationToken = default)
    {
        if (!input.IsMetricsRun)
            return;

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
                var windowSeconds = Math.Max(60, (int)Math.Ceiling(e2eSeconds + 1));
                var anyStage = false;
                foreach (var (stage, histogram) in StageHistograms)
                {
                    var snapshot = await QueryStageAsync(histogram, input.FacilityId, windowSeconds, input.FinishedAt, cancellationToken);
                    stages[stage] = snapshot;
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
                DurationMs = (long)Math.Round(e2eSeconds * 1000)
            },
            PrometheusWaitMs = (long)wait.TotalMilliseconds,
            Stages = stages,
            Throughput = new ThroughputSnapshot
            {
                PatientsPerMinute = e2eSeconds > 0 ? input.PatientCount / (e2eSeconds / 60.0) : 0,
                ResourcesPerSecond = e2eSeconds > 0 ? input.ManifestResourceCount / e2eSeconds : 0
            },
            E2eDurationSeconds = e2eSeconds,
            Validators = input.Validators.Select(v => new ValidatorOutcomeSnapshot
            {
                Name = v.Name,
                Outcome = v.Outcome,
                IssueCount = v.IssueCount
            }).ToList()
        };

        await _store.UpsertAsync(document, cancellationToken);
        _logger.LogInformation(
            "Persisted automation_run_metrics for run {RunId} (stagesUnavailable={Unavailable})",
            input.RunId,
            stagesUnavailable);
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
        string histogramBase,
        string facilityId,
        int windowSeconds,
        DateTimeOffset evaluationTime,
        CancellationToken cancellationToken)
    {
        var selector = $"{histogramBase}_bucket{{facility_id=\"{EscapePromLabel(facilityId)}\"}}[{windowSeconds}s]";
        var countSelector = $"sum(increase({histogramBase}_count{{facility_id=\"{EscapePromLabel(facilityId)}\"}}[{windowSeconds}s]))";

        var count = await _prometheus.QueryScalarAsync(countSelector, evaluationTime, cancellationToken);
        if (count is null or <= 0)
            return new StageLatencySnapshot { Unavailable = true };

        var p50 = await _prometheus.QueryScalarAsync(
            $"histogram_quantile(0.50, sum by (le) (increase({selector})))", evaluationTime, cancellationToken);
        var p95 = await _prometheus.QueryScalarAsync(
            $"histogram_quantile(0.95, sum by (le) (increase({selector})))", evaluationTime, cancellationToken);
        var p99 = await _prometheus.QueryScalarAsync(
            $"histogram_quantile(0.99, sum by (le) (increase({selector})))", evaluationTime, cancellationToken);

        return new StageLatencySnapshot
        {
            Unavailable = p50 is null && p95 is null && p99 is null,
            Count = count.Value,
            P50Ms = p50 ?? 0,
            P95Ms = p95 ?? 0,
            P99Ms = p99 ?? 0
        };
    }

    private static string EscapePromLabel(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
