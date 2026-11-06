using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.TestRail;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class RecordTestRailRunsTests
{
    [Fact]
    public async Task Saved_link_is_pushed_to_the_run_hub()
    {
        var (recorder, store, proxy, _) = Recorder();
        var state = Run(Guid.NewGuid());
        store.Setup(s => s.UpsertRunSummaryAsync(
                It.IsAny<AutomationRunSummary>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await recorder.RecordAsync(state.RunId, [Link(292)], state, Persist(store, state), Summary, CancellationToken.None);

        store.Verify(s => s.UpsertRunSummaryAsync(
            It.Is<AutomationRunSummary>(summary => summary.TestRailRuns.Any(run => run.RunId == 292)),
            state.FacilityId,
            state.ReportId,
            CancellationToken.None), Times.Once);
        proxy.Verify(p => p.SendCoreAsync(
            "status",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Saved_link_for_a_stored_run_is_pushed_after_the_write()
    {
        var (recorder, store, proxy, runId) = Recorder();
        store.Setup(s => s.GetRunSummaryAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Stored(runId));
        store.Setup(s => s.UpsertRunSummaryAsync(
                It.IsAny<AutomationRunSummary>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await recorder.RecordAsync(runId, [Link(292)], state: null, UnusedPersist, UnusedSummary, CancellationToken.None);

        store.Verify(s => s.UpsertRunSummaryAsync(
            It.Is<AutomationRunSummary>(summary => summary.TestRailRuns.Any(run => run.RunId == 292)),
            "facility",
            "report",
            CancellationToken.None), Times.Once);
        proxy.Verify(p => p.SendCoreAsync(
            "status",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Failed_in_memory_save_is_not_pushed()
    {
        var (recorder, store, proxy, _) = Recorder();
        var state = Run(Guid.NewGuid());
        var persistCalls = 0;

        await recorder.RecordAsync(
            state.RunId,
            [Link(292)],
            state,
            _ =>
            {
                persistCalls++;
                return Task.FromResult(false);
            },
            UnusedSummary,
            CancellationToken.None);

        persistCalls.Should().Be(1);
        state.TestRailRuns.Should().ContainSingle(run => run.RunId == 292);
        store.Verify(s => s.UpsertRunSummaryAsync(
            It.IsAny<AutomationRunSummary>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
        proxy.Verify(p => p.SendCoreAsync(
            It.IsAny<string>(),
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Failed_save_is_logged_and_not_pushed()
    {
        var logger = new ListLogger();
        var (recorder, store, proxy, runId) = Recorder(logger);
        store.Setup(s => s.GetRunSummaryAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Stored(runId));
        store.Setup(s => s.UpsertRunSummaryAsync(
                It.IsAny<AutomationRunSummary>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("mongo down"));

        await recorder.RecordAsync(runId, [Link(292)], state: null, UnusedPersist, UnusedSummary, CancellationToken.None);

        proxy.Verify(p => p.SendCoreAsync(
            It.IsAny<string>(),
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Never);
        logger.Errors.Should().Contain(message => message.Contains(runId.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Duplicate_run_id_is_not_written_again()
    {
        var (recorder, store, proxy, _) = Recorder();
        var state = Run(Guid.NewGuid());
        state.TestRailRuns.Add(Link(292));
        var persistCalls = 0;

        await recorder.RecordAsync(
            state.RunId,
            [Link(292)],
            state,
            _ =>
            {
                persistCalls++;
                return Task.FromResult(true);
            },
            UnusedSummary,
            CancellationToken.None);

        persistCalls.Should().Be(0);
        state.TestRailRuns.Should().ContainSingle();
        store.Verify(s => s.GetRunSummaryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        proxy.Verify(p => p.SendCoreAsync(
            It.IsAny<string>(),
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Empty_list_does_not_touch_the_store_or_the_hub()
    {
        var (recorder, store, proxy, runId) = Recorder();
        var persistCalls = 0;

        await recorder.RecordAsync(runId, [], state: null, _ =>
        {
            persistCalls++;
            return Task.FromResult(true);
        }, UnusedSummary, CancellationToken.None);
        await recorder.RecordAsync(runId, null, Run(runId), _ =>
        {
            persistCalls++;
            return Task.FromResult(true);
        }, UnusedSummary, CancellationToken.None);

        persistCalls.Should().Be(0);
        store.Verify(s => s.GetRunSummaryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        store.Verify(s => s.UpsertRunSummaryAsync(
            It.IsAny<AutomationRunSummary>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
        proxy.Verify(p => p.SendCoreAsync(
            It.IsAny<string>(),
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Missing_run_is_left_unchanged()
    {
        var (recorder, store, proxy, runId) = Recorder();
        store.Setup(s => s.GetRunSummaryAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AutomationRunSummary?)null);

        await recorder.RecordAsync(runId, [Link(292)], state: null, UnusedPersist, UnusedSummary, CancellationToken.None);

        store.Verify(s => s.UpsertRunSummaryAsync(
            It.IsAny<AutomationRunSummary>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
        proxy.Verify(p => p.SendCoreAsync(
            It.IsAny<string>(),
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Hub_failure_after_a_successful_save_is_logged_and_not_thrown()
    {
        var logger = new ListLogger();
        var (recorder, store, proxy, _) = Recorder(logger);
        var state = Run(Guid.NewGuid());
        store.Setup(s => s.UpsertRunSummaryAsync(
                It.IsAny<AutomationRunSummary>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        proxy.Setup(p => p.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("hub down"));

        var act = () => recorder.RecordAsync(state.RunId, [Link(292)], state, Persist(store, state), Summary, CancellationToken.None);

        await act.Should().NotThrowAsync();
        store.Verify(s => s.UpsertRunSummaryAsync(
            It.IsAny<AutomationRunSummary>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
        logger.Warnings.Should().Contain(message => message.Contains(state.RunId.ToString(), StringComparison.Ordinal));
    }

    private static Func<MutableRunState, Task<bool>> Persist(Mock<ISnapshotStore> store, MutableRunState state) =>
        async _ =>
        {
            await store.Object.UpsertRunSummaryAsync(Summary(state), state.FacilityId, state.ReportId, CancellationToken.None);
            return true;
        };

    private static Task<bool> UnusedPersist(MutableRunState state) =>
        throw new InvalidOperationException("In-memory persist is not used for a stored run.");

    private static AutomationRunSummary UnusedSummary(MutableRunState state) =>
        throw new InvalidOperationException("In-memory summary is not used for a stored run.");

    private static (TestRailRunLinkRecorder Recorder, Mock<ISnapshotStore> Store, Mock<IClientProxy> Proxy, Guid RunId) Recorder(ListLogger? logger = null)
    {
        var store = new Mock<ISnapshotStore>();
        var proxy = HubProxy();
        var runId = Guid.NewGuid();
        var recorder = new TestRailRunLinkRecorder(Hub(proxy), logger ?? new ListLogger(), store.Object);
        return (recorder, store, proxy, runId);
    }

    private static MutableRunState Run(Guid runId) => new(
        runId,
        scenarioId: null,
        AutomationScenarioKind.Custom,
        new ResolvedRunOptions(
            PatientCount: 1,
            ResourcesPerPatient: 1,
            Seed: 1,
            PollingIntervalSeconds: 3,
            MaxPollingDurationMinutes: 0,
            LokiScrapeWindowMinutes: 30,
            CleanupServiceData: false,
            CleanupFhirData: true,
            SelectedMeasures: [],
            PatientProfiles: [],
            PatientCohorts: []),
        runNameOverride: null,
        runConfigurationJson: null)
    {
        FacilityId = "facility",
        ReportId = "report",
        Status = AutomationRunStatus.Succeeded
    };

    private static AutomationRunSummary Stored(Guid runId) => new()
    {
        RunId = runId,
        FacilityId = "facility",
        ReportId = "report",
        Status = AutomationRunStatus.Succeeded
    };

    private static AutomationRunSummary Summary(MutableRunState state) => new()
    {
        RunId = state.RunId,
        FacilityId = state.FacilityId,
        ReportId = state.ReportId,
        Status = state.Status,
        TestRailRuns = TestRailRunLinks.Copy(state.TestRailRuns)
    };

    private sealed class ListLogger : ILogger
    {
        public List<string> Errors { get; } = [];
        public List<string> Warnings { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
                Errors.Add(formatter(state, exception));
            else if (logLevel == LogLevel.Warning)
                Warnings.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private static Mock<IClientProxy> HubProxy()
    {
        var proxy = new Mock<IClientProxy>();
        proxy.Setup(p => p.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return proxy;
    }

    private static IHubContext<RunHub> Hub(Mock<IClientProxy> proxy)
    {
        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(proxy.Object);
        var hub = new Mock<IHubContext<RunHub>>();
        hub.SetupGet(h => h.Clients).Returns(clients.Object);
        return hub.Object;
    }

    private static TestRailRunReference Link(int runId) =>
        TestRailRunLinks.Capture(
            "https://example.testrail.io",
            runId,
            [new TestRailResultDto { Id = 1, TestId = 16898 }])!;
}
