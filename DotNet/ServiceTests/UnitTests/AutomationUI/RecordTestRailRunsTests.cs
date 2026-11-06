using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.TestRail;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class RecordTestRailRunsTests
{
    [Fact]
    public async Task Saved_link_is_pushed_to_the_run_hub()
    {
        var (manager, store, proxy, runId) = Manager();
        store.Setup(s => s.UpsertRunSummaryAsync(
                It.IsAny<AutomationRunSummary>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await manager.RecordTestRailRunsAsync(runId, [Link(292)]);

        store.Verify(s => s.UpsertRunSummaryAsync(
            It.Is<AutomationRunSummary>(summary => summary.TestRailRuns.Any(run => run.RunId == 292)),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
        proxy.Verify(p => p.SendCoreAsync(
            "status",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Failed_save_is_logged_and_not_pushed()
    {
        var store = new Mock<ISnapshotStore>();
        var proxy = HubProxy();
        var logger = new ListLogger();
        var manager = new AutomationRunManager(Hub(proxy), logger, store.Object);
        var runId = Guid.NewGuid();
        manager.TrackRun(Run(runId));
        store.Setup(s => s.UpsertRunSummaryAsync(
                It.IsAny<AutomationRunSummary>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("mongo down"));

        await manager.RecordTestRailRunsAsync(runId, [Link(292)]);

        store.Verify(s => s.UpsertRunSummaryAsync(
            It.IsAny<AutomationRunSummary>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
        proxy.Verify(p => p.SendCoreAsync(
            It.IsAny<string>(),
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Never);
        logger.Errors.Should().Contain(message => message.Contains(runId.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Stored_run_that_is_not_in_memory_skips_the_hub_when_the_save_fails()
    {
        var store = new Mock<ISnapshotStore>();
        var proxy = HubProxy();
        var manager = new AutomationRunManager(Hub(proxy), NullLogger<AutomationRunManager>.Instance, store.Object);
        var runId = Guid.NewGuid();
        store.Setup(s => s.GetRunSummaryAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutomationRunSummary
            {
                RunId = runId,
                FacilityId = "facility",
                ReportId = "report",
                Status = AutomationRunStatus.Succeeded
            });
        store.Setup(s => s.UpsertRunSummaryAsync(
                It.IsAny<AutomationRunSummary>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("mongo down"));

        await manager.RecordTestRailRunsAsync(runId, [Link(292)]);

        proxy.Verify(p => p.SendCoreAsync(
            It.IsAny<string>(),
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (AutomationRunManager Manager, Mock<ISnapshotStore> Store, Mock<IClientProxy> Proxy, Guid RunId) Manager()
    {
        var store = new Mock<ISnapshotStore>();
        var proxy = HubProxy();
        var manager = new AutomationRunManager(Hub(proxy), NullLogger<AutomationRunManager>.Instance, store.Object);
        var runId = Guid.NewGuid();
        manager.TrackRun(Run(runId));
        return (manager, store, proxy, runId);
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

    private sealed class ListLogger : ILogger<AutomationRunManager>
    {
        public List<string> Errors { get; } = [];

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
