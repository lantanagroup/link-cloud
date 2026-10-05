using System.Reflection;
using Automation.UI.Services;
using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Automation.Link.Models;
using LantanaGroup.Link.Sdk.Clients;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using FluentAssertions;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

public class RunSnapshotOrchestratorTests
{
    [Fact]
    public async Task UpdateRunAsync_waits_for_finalization_before_clearing_snapshots()
    {
        var runId = Guid.NewGuid();
        var reportId = Guid.NewGuid().ToString();
        var inScheduleRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSchedule = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var metaUpdates = 0;

        var store = new Mock<ISnapshotStore>();
        store.Setup(s => s.RegisterRunAsync(It.IsAny<Guid>(), It.IsAny<RunSnapshotMeta>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.SetDomainAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<It.IsAnyType>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.AppendLogsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.CompleteRunAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.GetRunMetaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RunSnapshotMeta?)null);
        store.Setup(s => s.GetDomainAsync<PipelineDataReader.ReportScheduleInfo>(It.IsAny<Guid>(), "schedule", It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                inScheduleRead.TrySetResult();
                await releaseSchedule.Task;
                return null;
            });
        store.Setup(s => s.UpdateRunMetaAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                Interlocked.Increment(ref metaUpdates);
                return Task.CompletedTask;
            });

        var reader = new PipelineDataReader(
            Mock.Of<IReportServiceClient>(),
            Mock.Of<IDataAcquisitionServiceClient>(),
            Mock.Of<INormalizationServiceClient>(),
            Mock.Of<IFacilityServiceClient>());
        var scopeProvider = new Mock<IServiceProvider>();
        scopeProvider.Setup(provider => provider.GetService(typeof(PipelineDataReader))).Returns(reader);
        var scope = new Mock<IServiceScope>();
        scope.SetupGet(s => s.ServiceProvider).Returns(scopeProvider.Object);
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(factory => factory.CreateScope()).Returns(scope.Object);
        var services = new Mock<IServiceProvider>();
        services.Setup(provider => provider.GetService(typeof(IServiceScopeFactory))).Returns(scopeFactory.Object);

        var orchestrator = new RunSnapshotOrchestrator(store.Object, services.Object, NullLogger<RunSnapshotOrchestrator>.Instance);
        await orchestrator.RegisterRunAsync(runId, "facility", reportId);

        var completing = orchestrator.CompleteRunAsync(runId);
        await inScheduleRead.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var updating = orchestrator.UpdateRunAsync(runId, "facility", Guid.NewGuid().ToString());
        await Task.Delay(150);
        metaUpdates.Should().Be(0);

        releaseSchedule.TrySetResult();
        await completing.WaitAsync(TimeSpan.FromSeconds(10));
        await updating.WaitAsync(TimeSpan.FromSeconds(10));
        metaUpdates.Should().Be(1);

        await orchestrator.CompleteRunAsync(runId);
    }

    [Fact]
    public async Task StopAsync_a_second_call_waits_for_the_first()
    {
        var store = new Mock<ISnapshotStore>();
        store.Setup(s => s.RegisterRunAsync(It.IsAny<Guid>(), It.IsAny<RunSnapshotMeta>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var orchestrator = CreateOrchestrator(store);
        var runId = Guid.NewGuid();
        await orchestrator.RegisterRunAsync(runId, "facility", Guid.NewGuid().ToString());

        var pollers = typeof(RunSnapshotOrchestrator)
            .GetField("_activePollers", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(orchestrator)!;
        var handle = pollers.GetType().GetProperty("Item")!.GetValue(pollers, new object[] { runId })!;
        var stop = handle.GetType().GetMethod("StopAsync")!;
        var first = (Task)stop.Invoke(handle, null)!;
        var second = (Task)stop.Invoke(handle, null)!;

        await Task.WhenAll(first, second);
    }

    [Fact]
    public async Task UpdateRunAsync_keeps_reconcile_from_starting_the_old_report()
    {
        var runId = Guid.NewGuid();
        var oldReport = Guid.NewGuid().ToString();
        var newReport = Guid.NewGuid().ToString();
        var meta = new RunSnapshotMeta
        {
            RunId = runId,
            FacilityId = "facility",
            ReportId = oldReport,
            StartedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var store = new Mock<ISnapshotStore>();
        store.Setup(s => s.RegisterRunAsync(It.IsAny<Guid>(), It.IsAny<RunSnapshotMeta>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.SetDomainAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<It.IsAnyType>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.AppendLogsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.GetActiveRunsAsync(It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<IReadOnlyList<RunSnapshotMeta>>(new List<RunSnapshotMeta> { meta }));
        store.Setup(s => s.GetRunMetaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<RunSnapshotMeta?>(meta));
        store.Setup(s => s.UpdateRunMetaAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (Guid _, string facilityId, string reportId, CancellationToken _) =>
            {
                entered.TrySetResult();
                await release.Task;
                meta = meta with { FacilityId = facilityId, ReportId = reportId };
            });

        var orchestrator = CreateOrchestrator(store);
        await orchestrator.RegisterRunAsync(runId, "facility", oldReport);

        var updating = orchestrator.UpdateRunAsync(runId, "facility", newReport);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var reconcile = typeof(RunSnapshotOrchestrator).GetMethod("ReconcileAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var reconciling = (Task)reconcile.Invoke(orchestrator, new object[] { CancellationToken.None })!;
        await Task.Delay(300);
        reconciling.IsCompleted.Should().BeFalse();
        PollerCount(orchestrator).Should().Be(0);

        release.TrySetResult();
        await updating.WaitAsync(TimeSpan.FromSeconds(10));
        await reconciling.WaitAsync(TimeSpan.FromSeconds(10));

        PollerCount(orchestrator).Should().Be(1);
        PollerReportId(orchestrator, runId).Should().Be(newReport);
    }

    [Fact]
    public async Task ReconcileAsync_keeps_a_poller_whose_run_is_still_active()
    {
        var runId = Guid.NewGuid();
        var meta = new RunSnapshotMeta
        {
            RunId = runId,
            FacilityId = "facility",
            ReportId = "report",
            StartedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };
        var store = new Mock<ISnapshotStore>();
        store.Setup(s => s.RegisterRunAsync(It.IsAny<Guid>(), It.IsAny<RunSnapshotMeta>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.SetDomainAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<It.IsAnyType>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.AppendLogsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.GetActiveRunsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RunSnapshotMeta>());
        store.Setup(s => s.GetRunMetaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(meta);

        var orchestrator = CreateOrchestrator(store);
        await orchestrator.RegisterRunAsync(runId, "facility", "report");

        var reconcile = typeof(RunSnapshotOrchestrator).GetMethod("ReconcileAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await ((Task)reconcile.Invoke(orchestrator, new object[] { CancellationToken.None })!).WaitAsync(TimeSpan.FromSeconds(10));

        PollerCount(orchestrator).Should().Be(1);
    }

    [Fact]
    public async Task ReconcileAsync_stops_a_poller_whose_run_is_no_longer_active()
    {
        var runId = Guid.NewGuid();
        var store = new Mock<ISnapshotStore>();
        store.Setup(s => s.RegisterRunAsync(It.IsAny<Guid>(), It.IsAny<RunSnapshotMeta>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.SetDomainAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<It.IsAnyType>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.AppendLogsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.GetActiveRunsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RunSnapshotMeta>());
        store.Setup(s => s.GetRunMetaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RunSnapshotMeta?)null);

        var orchestrator = CreateOrchestrator(store);
        await orchestrator.RegisterRunAsync(runId, "facility", "report");

        var reconcile = typeof(RunSnapshotOrchestrator).GetMethod("ReconcileAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await ((Task)reconcile.Invoke(orchestrator, new object[] { CancellationToken.None })!).WaitAsync(TimeSpan.FromSeconds(10));

        PollerCount(orchestrator).Should().Be(0);
    }

    [Fact]
    public async Task CompleteRunAsync_shares_an_in_flight_completion()
    {
        var calls = 0;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new Mock<ISnapshotStore>();
        store.Setup(s => s.GetDomainAsync<PipelineDataReader.ReportScheduleInfo>(It.IsAny<Guid>(), "schedule", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainSnapshot<PipelineDataReader.ReportScheduleInfo>?)null);
        store.Setup(s => s.CompleteRunAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                Interlocked.Increment(ref calls);
                started.TrySetResult();
                await release.Task;
            });

        var orchestrator = CreateOrchestrator(store);
        var runId = Guid.NewGuid();
        var first = orchestrator.CompleteRunAsync(runId);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var second = orchestrator.CompleteRunAsync(runId);
        await Task.Delay(50);
        calls.Should().Be(1);

        release.TrySetResult();
        await Task.WhenAll(first, second);
        calls.Should().Be(1);
    }

    [Fact]
    public async Task CompleteRunAsync_shares_a_completion_failure()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new Mock<ISnapshotStore>();
        store.Setup(s => s.GetDomainAsync<PipelineDataReader.ReportScheduleInfo>(It.IsAny<Guid>(), "schedule", It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                started.TrySetResult();
                await release.Task;
                return (DomainSnapshot<PipelineDataReader.ReportScheduleInfo>?)null;
            });
        store.Setup(s => s.CompleteRunAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("completion failed"));

        var orchestrator = CreateOrchestrator(store);
        var runId = Guid.NewGuid();
        var first = orchestrator.CompleteRunAsync(runId);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var second = orchestrator.CompleteRunAsync(runId);
        release.TrySetResult();

        var firstError = await Assert.ThrowsAsync<InvalidOperationException>(() => first);
        var secondError = await Assert.ThrowsAsync<InvalidOperationException>(() => second);
        firstError.Should().BeSameAs(secondError);
        store.Verify(s => s.CompleteRunAsync(runId, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompleteRunAsync_waits_for_a_report_switch_before_marking_the_run_inactive()
    {
        var runId = Guid.NewGuid();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completions = 0;
        var store = new Mock<ISnapshotStore>();
        store.Setup(s => s.RegisterRunAsync(It.IsAny<Guid>(), It.IsAny<RunSnapshotMeta>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.SetDomainAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<It.IsAnyType>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.AppendLogsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.GetRunMetaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RunSnapshotMeta?)null);
        store.Setup(s => s.GetDomainAsync<PipelineDataReader.ReportScheduleInfo>(It.IsAny<Guid>(), "schedule", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainSnapshot<PipelineDataReader.ReportScheduleInfo>?)null);
        store.Setup(s => s.UpdateRunMetaAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (Guid _, string _, string _, CancellationToken _) =>
            {
                entered.TrySetResult();
                await release.Task;
            });
        store.Setup(s => s.CompleteRunAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                Interlocked.Increment(ref completions);
                return Task.CompletedTask;
            });

        var orchestrator = CreateOrchestrator(store);
        await orchestrator.RegisterRunAsync(runId, "facility", Guid.NewGuid().ToString());
        var updating = orchestrator.UpdateRunAsync(runId, "facility", Guid.NewGuid().ToString());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var completing = orchestrator.CompleteRunAsync(runId);
        await Task.Delay(300);
        completing.IsCompleted.Should().BeFalse();
        completions.Should().Be(0);

        release.TrySetResult();
        await updating.WaitAsync(TimeSpan.FromSeconds(10));
        await completing.WaitAsync(TimeSpan.FromSeconds(10));
        completions.Should().Be(1);
        PollerCount(orchestrator).Should().Be(0);
    }

    private static int PollerCount(RunSnapshotOrchestrator orchestrator)
    {
        var pollers = typeof(RunSnapshotOrchestrator)
            .GetField("_activePollers", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(orchestrator)!;
        return (int)pollers.GetType().GetProperty("Count")!.GetValue(pollers)!;
    }

    private static string PollerReportId(RunSnapshotOrchestrator orchestrator, Guid runId)
    {
        var pollers = typeof(RunSnapshotOrchestrator)
            .GetField("_activePollers", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(orchestrator)!;
        var handle = pollers.GetType().GetProperty("Item")!.GetValue(pollers, new object[] { runId })!;
        return (string)handle.GetType().GetProperty("ReportId")!.GetValue(handle)!;
    }

    private static RunSnapshotOrchestrator CreateOrchestrator(Mock<ISnapshotStore> store)
    {
        var reader = new PipelineDataReader(
            Mock.Of<IReportServiceClient>(),
            Mock.Of<IDataAcquisitionServiceClient>(),
            Mock.Of<INormalizationServiceClient>(),
            Mock.Of<IFacilityServiceClient>());
        var scopeProvider = new Mock<IServiceProvider>();
        scopeProvider.Setup(provider => provider.GetService(typeof(PipelineDataReader))).Returns(reader);
        var scope = new Mock<IServiceScope>();
        scope.SetupGet(s => s.ServiceProvider).Returns(scopeProvider.Object);
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(factory => factory.CreateScope()).Returns(scope.Object);
        var services = new Mock<IServiceProvider>();
        services.Setup(provider => provider.GetService(typeof(IServiceScopeFactory))).Returns(scopeFactory.Object);
        return new RunSnapshotOrchestrator(store.Object, services.Object, NullLogger<RunSnapshotOrchestrator>.Instance);
    }
}
