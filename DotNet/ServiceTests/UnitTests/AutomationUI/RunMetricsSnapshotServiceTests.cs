using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class RunMetricsSnapshotServiceTests
{
    [Fact]
    public void Pipeline_window_uses_report_created_to_abs_submit()
    {
        var runStart = new DateTimeOffset(2026, 9, 2, 17, 33, 26, TimeSpan.Zero);
        var runEnd = runStart.AddSeconds(322);
        var created = new DateTime(2026, 9, 2, 17, 34, 0, DateTimeKind.Utc);
        var submitted = new DateTime(2026, 9, 2, 17, 35, 41, DateTimeKind.Utc);

        var window = RunMetricsSnapshotService.ResolvePipelineWindow(runStart, runEnd, created, submitted);

        window.StartedAt.Should().Be(new DateTimeOffset(created, TimeSpan.Zero));
        window.FinishedAt.Should().Be(new DateTimeOffset(submitted, TimeSpan.Zero));
        (window.FinishedAt - window.StartedAt).TotalSeconds.Should().Be(101);
    }

    [Fact]
    public void Pipeline_window_falls_back_to_run_clock_when_submit_is_missing()
    {
        var runStart = new DateTimeOffset(2026, 9, 2, 17, 33, 26, TimeSpan.Zero);
        var runEnd = runStart.AddSeconds(322);

        var window = RunMetricsSnapshotService.ResolvePipelineWindow(runStart, runEnd, runStart.UtcDateTime, null);

        window.StartedAt.Should().Be(runStart);
        window.FinishedAt.Should().Be(runEnd);
    }

    [Fact]
    public async Task Capture_stores_pipeline_window_not_cleanup_or_prom_wait()
    {
        var store = new Mock<IRunMetricsStore>();
        AutomationRunMetricsDocument? saved = null;
        store.Setup(s => s.UpsertAsync(It.IsAny<AutomationRunMetricsDocument>(), It.IsAny<CancellationToken>()))
            .Callback<AutomationRunMetricsDocument, CancellationToken>((doc, _) => saved = doc)
            .Returns(Task.CompletedTask);
        var service = CreateService(store.Object, new TelemetrySettings());
        var runStart = new DateTimeOffset(2026, 9, 2, 17, 33, 26, TimeSpan.Zero);
        var created = new DateTime(2026, 9, 2, 17, 34, 0, DateTimeKind.Utc);
        var submitted = new DateTime(2026, 9, 2, 17, 35, 41, DateTimeKind.Utc);

        await service.CaptureAsync(Input(
            isMetricsRun: true,
            startedAt: runStart,
            finishedAt: runStart.AddSeconds(322),
            reportCreatedAt: created,
            submittedAt: submitted));

        saved.Should().NotBeNull();
        saved!.Outcome.Should().Be("Succeeded");
        saved.E2eDurationSeconds.Should().Be(101);
        saved.StartedAt.Should().Be(new DateTimeOffset(created, TimeSpan.Zero));
        saved.FinishedAt.Should().Be(new DateTimeOffset(submitted, TimeSpan.Zero));
    }

    [Fact]
    public void Stage_histograms_omit_query_dispatch()
    {
        RunMetricsSnapshotService.StageHistograms.Select(s => s.Stage)
            .Should().Equal("acquisition", "normalization", "measureeval", "validation", "submission");
    }

    [Fact]
    public void Prometheus_wait_defaults_to_71_seconds()
    {
        var previous = Environment.GetEnvironmentVariable("OTEL_METRIC_EXPORT_INTERVAL");
        try
        {
            Environment.SetEnvironmentVariable("OTEL_METRIC_EXPORT_INTERVAL", null);
            RunMetricsSnapshotService.ResolvePrometheusWait().Should().Be(TimeSpan.FromSeconds(71));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OTEL_METRIC_EXPORT_INTERVAL", previous);
        }
    }

    [Fact]
    public void Prometheus_wait_honors_export_interval_env()
    {
        var previous = Environment.GetEnvironmentVariable("OTEL_METRIC_EXPORT_INTERVAL");
        try
        {
            Environment.SetEnvironmentVariable("OTEL_METRIC_EXPORT_INTERVAL", "15000");
            RunMetricsSnapshotService.ResolvePrometheusWait().Should().Be(TimeSpan.FromSeconds(26));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OTEL_METRIC_EXPORT_INTERVAL", previous);
        }
    }

    [Fact]
    public async Task Capture_skips_non_metrics_runs()
    {
        var store = new Mock<IRunMetricsStore>(MockBehavior.Strict);
        var service = CreateService(store.Object, new TelemetrySettings());

        await service.CaptureAsync(Input(isMetricsRun: false));

        store.Verify(s => s.UpsertAsync(It.IsAny<AutomationRunMetricsDocument>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Capture_without_prom_endpoint_persists_wall_clock_and_marks_stages_unavailable()
    {
        var store = new Mock<IRunMetricsStore>();
        AutomationRunMetricsDocument? saved = null;
        store.Setup(s => s.UpsertAsync(It.IsAny<AutomationRunMetricsDocument>(), It.IsAny<CancellationToken>()))
            .Callback<AutomationRunMetricsDocument, CancellationToken>((doc, _) => saved = doc)
            .Returns(Task.CompletedTask);
        var metrics = new Mock<IAutomationUiMetrics>();
        var service = CreateService(store.Object, new TelemetrySettings { PrometheusQueryEndpoint = "  " }, metrics.Object);

        await service.CaptureAsync(Input(isMetricsRun: true, patientCount: 10, startedAt: DateTimeOffset.UtcNow.AddMinutes(-2)));

        saved.Should().NotBeNull();
        saved!.PatientCount.Should().Be(10);
        saved.E2eDurationSeconds.Should().BeGreaterThan(0);
        saved.Stages.Should().NotBeEmpty();
        saved.Stages.Values.Should().OnlyContain(s => s.Unavailable);
        saved.Thetis.Generator.Should().Be("thetis");
        saved.Thetis.Seed.Should().Be(20260329);
        saved.Benchmark.Pass.Should().BeTrue();
        metrics.Verify(m => m.IncrementSnapshotMissing(), Times.Once);
    }

    [Fact]
    public async Task Capture_applies_target_duration_slo()
    {
        var store = new Mock<IRunMetricsStore>();
        AutomationRunMetricsDocument? saved = null;
        store.Setup(s => s.UpsertAsync(It.IsAny<AutomationRunMetricsDocument>(), It.IsAny<CancellationToken>()))
            .Callback<AutomationRunMetricsDocument, CancellationToken>((doc, _) => saved = doc)
            .Returns(Task.CompletedTask);

        var service = CreateService(store.Object, new TelemetrySettings());
        await service.CaptureAsync(Input(isMetricsRun: true, startedAt: DateTimeOffset.UtcNow.AddMinutes(-5), targetDurationSeconds: 30));

        saved.Should().NotBeNull();
        saved!.Benchmark.Pass.Should().BeFalse();
        saved.Benchmark.Violations.Should().Contain(v => v.Contains("Total run time"));
    }

    [Fact]
    public async Task Capture_with_prom_data_fills_stage_quantiles()
    {
        var store = new Mock<IRunMetricsStore>();
        AutomationRunMetricsDocument? saved = null;
        store.Setup(s => s.UpsertAsync(It.IsAny<AutomationRunMetricsDocument>(), It.IsAny<CancellationToken>()))
            .Callback<AutomationRunMetricsDocument, CancellationToken>((doc, _) => saved = doc)
            .Returns(Task.CompletedTask);
        var prom = new Mock<IPrometheusHistogramClient>();
        prom.Setup(p => p.IsReachableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        prom.Setup(p => p.QueryScalarAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string query, DateTimeOffset _, CancellationToken _) =>
                query.Contains("histogram_quantile(0.95", StringComparison.Ordinal) ? 1234
                : query.Contains("histogram_quantile(0.50", StringComparison.Ordinal) ? 100
                : query.Contains("histogram_quantile(0.99", StringComparison.Ordinal) ? 2000
                : query.Contains("_count{", StringComparison.Ordinal) ? 8
                : null);
        var metrics = new Mock<IAutomationUiMetrics>();
        var service = CreateService(
            store.Object,
            new TelemetrySettings { PrometheusQueryEndpoint = "http://prometheus:9090" },
            metrics.Object,
            prom.Object,
            new ImmediateTimeProvider());

        await service.CaptureAsync(Input(isMetricsRun: true));

        saved.Should().NotBeNull();
        saved!.Stages["acquisition"].Unavailable.Should().BeFalse();
        saved.Stages["acquisition"].Count.Should().Be(8);
        saved.Stages["acquisition"].P50Ms.Should().Be(100);
        saved.Stages["acquisition"].P95Ms.Should().Be(1234);
        saved.ScenarioFingerprint.Should().NotBeNullOrWhiteSpace();
        saved.ScenarioVersion.Should().Be(1);
        saved.Thetis.DurationMs.Should().Be(0);
        metrics.Verify(m => m.IncrementSnapshotMissing(), Times.Never);
        prom.Verify(p => p.QueryScalarAsync(
            It.Is<string>(q => q.Contains("increase(", StringComparison.Ordinal)),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
        prom.Verify(p => p.QueryScalarAsync(
            It.Is<string>(q => q.StartsWith("sum(link_data_acq_query_duration_milliseconds_count{", StringComparison.Ordinal)),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        prom.Verify(p => p.QueryScalarAsync(
            It.Is<string>(q => q.Contains("histogram_quantile(0.95, sum by (le) (link_data_acq_query_duration_milliseconds_bucket{", StringComparison.Ordinal)
                && !q.Contains("[", StringComparison.Ordinal)),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Capture_marks_stage_unavailable_when_prom_count_is_zero()
    {
        var store = new Mock<IRunMetricsStore>();
        AutomationRunMetricsDocument? saved = null;
        store.Setup(s => s.UpsertAsync(It.IsAny<AutomationRunMetricsDocument>(), It.IsAny<CancellationToken>()))
            .Callback<AutomationRunMetricsDocument, CancellationToken>((doc, _) => saved = doc)
            .Returns(Task.CompletedTask);
        var prom = new Mock<IPrometheusHistogramClient>();
        prom.Setup(p => p.IsReachableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        prom.Setup(p => p.QueryScalarAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        var service = CreateService(
            store.Object,
            new TelemetrySettings { PrometheusQueryEndpoint = "http://prometheus:9090" },
            Mock.Of<IAutomationUiMetrics>(),
            prom.Object,
            new ImmediateTimeProvider());

        await service.CaptureAsync(Input(isMetricsRun: true));

        saved.Should().NotBeNull();
        saved!.Stages.Values.Should().OnlyContain(s => s.Unavailable);
    }

    [Fact]
    public async Task Capture_queries_prometheus_after_the_wait_not_at_finish_time()
    {
        var finishedAt = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset? queriedAt = null;
        var prom = new Mock<IPrometheusHistogramClient>();
        prom.Setup(p => p.IsReachableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        prom.Setup(p => p.QueryScalarAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback<string, DateTimeOffset, CancellationToken>((_, time, _) => queriedAt = time)
            .ReturnsAsync(1);
        var store = new Mock<IRunMetricsStore>();
        store.Setup(s => s.UpsertAsync(It.IsAny<AutomationRunMetricsDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var clock = new FrozenTimeProvider(finishedAt.AddSeconds(71));
        var service = CreateService(
            store.Object,
            new TelemetrySettings { PrometheusQueryEndpoint = "http://prometheus:9090" },
            Mock.Of<IAutomationUiMetrics>(),
            prom.Object,
            clock);

        await service.CaptureAsync(Input(isMetricsRun: true, startedAt: finishedAt.AddMinutes(-2), finishedAt: finishedAt));

        queriedAt.Should().NotBeNull();
        queriedAt!.Value.Should().BeAfter(finishedAt);
    }

    [Fact]
    public async Task Capture_when_prometheus_unreachable_skips_queries_and_marks_stages_unavailable()
    {
        var store = new Mock<IRunMetricsStore>();
        AutomationRunMetricsDocument? saved = null;
        store.Setup(s => s.UpsertAsync(It.IsAny<AutomationRunMetricsDocument>(), It.IsAny<CancellationToken>()))
            .Callback<AutomationRunMetricsDocument, CancellationToken>((doc, _) => saved = doc)
            .Returns(Task.CompletedTask);
        var prom = new Mock<IPrometheusHistogramClient>(MockBehavior.Strict);
        prom.Setup(p => p.IsReachableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var service = CreateService(
            store.Object,
            new TelemetrySettings { PrometheusQueryEndpoint = "http://prometheus:9090" },
            Mock.Of<IAutomationUiMetrics>(),
            prom.Object,
            new ImmediateTimeProvider());

        await service.CaptureAsync(Input(isMetricsRun: true));

        saved.Should().NotBeNull();
        saved!.Stages.Values.Should().OnlyContain(s => s.Unavailable);
        prom.Verify(p => p.QueryScalarAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static RunMetricsSnapshotService CreateService(
        IRunMetricsStore store,
        TelemetrySettings telemetry,
        IAutomationUiMetrics? metrics = null,
        IPrometheusHistogramClient? prometheus = null,
        TimeProvider? time = null)
    {
        return new RunMetricsSnapshotService(
            store,
            prometheus ?? Mock.Of<IPrometheusHistogramClient>(),
            metrics ?? Mock.Of<IAutomationUiMetrics>(),
            Options.Create(telemetry),
            NullLogger<RunMetricsSnapshotService>.Instance,
            time);
    }

    private static RunMetricsCaptureInput Input(
        bool isMetricsRun,
        int patientCount = 4,
        DateTimeOffset? startedAt = null,
        int? targetDurationSeconds = null,
        DateTimeOffset? finishedAt = null,
        DateTime? reportCreatedAt = null,
        DateTime? submittedAt = null)
    {
        var finished = finishedAt ?? DateTimeOffset.UtcNow;
        return new RunMetricsCaptureInput(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "perf-scenario",
            "adhoc-10p",
            isMetricsRun,
            "Succeeded",
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            startedAt ?? finished.AddMinutes(-1),
            finished,
            20260329,
            patientCount,
            50,
            100,
            400,
            [
                new PipelineSummarySnapshotBuilder.ValidatorResultSnapshot
                {
                    Name = "REPORT DATABASE VALIDATION",
                    Outcome = "Passed",
                    IssueCount = 0
                }
            ],
            targetDurationSeconds,
            ReportCreatedAt: reportCreatedAt,
            SubmittedAt: submittedAt);
    }

    private sealed class ImmediateTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            callback(state);
            return new CompletedTimer();
        }

        private sealed class CompletedTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class FrozenTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            callback(state);
            return new NopTimer();
        }

        private sealed class NopTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
