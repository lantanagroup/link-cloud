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
    public async Task StopAsync_a_second_call_returns_without_waiting()
    {
        var store = new Mock<ISnapshotStore>();
        var inPoll = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releasePoll = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.Setup(s => s.RegisterRunAsync(It.IsAny<Guid>(), It.IsAny<RunSnapshotMeta>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.GetRunMetaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                inPoll.TrySetResult();
                await releasePoll.Task;
                return (RunSnapshotMeta?)null;
            });
        store.Setup(s => s.SetDomainAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<It.IsAnyType>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.AppendLogsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var orchestrator = CreateOrchestrator(store);
        var runId = Guid.NewGuid();
        await orchestrator.RegisterRunAsync(runId, "facility", Guid.NewGuid().ToString());

        await inPoll.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var pollers = typeof(RunSnapshotOrchestrator)
            .GetField("_activePollers", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(orchestrator)!;
        var handle = pollers.GetType().GetProperty("Item")!.GetValue(pollers, new object[] { runId })!;
        var stop = handle.GetType().GetMethod("StopAsync")!;
        var first = (Task)stop.Invoke(handle, null)!;
        var second = (Task)stop.Invoke(handle, null)!;

        first.IsCompleted.Should().BeFalse();
        second.IsCompleted.Should().BeTrue();

        releasePoll.TrySetResult();
        await first.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task UpdateRunAsync_keeps_reconcile_from_starting_the_old_report()
    {
        var runId = Guid.NewGuid();
        var otherRunId = Guid.NewGuid();
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
        var otherMeta = new RunSnapshotMeta
        {
            RunId = otherRunId,
            FacilityId = "facility",
            ReportId = "other-report",
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
            .Returns(() => Task.FromResult<IReadOnlyList<RunSnapshotMeta>>(new List<RunSnapshotMeta> { meta, otherMeta }));
        store.Setup(s => s.GetRunMetaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, CancellationToken _) => Task.FromResult<RunSnapshotMeta?>(id == otherRunId ? otherMeta : meta));
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
        await reconciling.WaitAsync(TimeSpan.FromSeconds(10));
        PollerCount(orchestrator).Should().Be(1);
        PollerReportId(orchestrator, otherRunId).Should().Be("other-report");

        release.TrySetResult();
        await updating.WaitAsync(TimeSpan.FromSeconds(10));

        PollerCount(orchestrator).Should().Be(2);
        PollerReportId(orchestrator, runId).Should().Be(newReport);
        PollerReportId(orchestrator, otherRunId).Should().Be("other-report");
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

    [Fact]
    public async Task StopAllPollersAsync_stops_a_replacement_added_after_shutdown_sees_the_old_poller()
    {
        var runId = Guid.NewGuid();
        var sawOldPoller = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseShutdown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var created = 0;
        var disposed = 0;

        var store = new Mock<ISnapshotStore>();
        store.Setup(s => s.RegisterRunAsync(It.IsAny<Guid>(), It.IsAny<RunSnapshotMeta>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.SetDomainAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<It.IsAnyType>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.AppendLogsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.GetRunMetaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RunSnapshotMeta?)null);
        store.Setup(s => s.UpdateRunMetaAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.Dispose()).Callback(() => Interlocked.Increment(ref disposed));
        var orchestrator = CreateOrchestrator(store, scope, () => Interlocked.Increment(ref created));
        orchestrator.AfterShutdownSnapshot = async () =>
        {
            sawOldPoller.TrySetResult();
            await releaseShutdown.Task;
        };
        await orchestrator.RegisterRunAsync(runId, "facility", Guid.NewGuid().ToString());

        var stopAll = typeof(RunSnapshotOrchestrator).GetMethod("StopAllPollersAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var stopping = (Task)stopAll.Invoke(orchestrator, new object[] { CancellationToken.None })!;
        await sawOldPoller.Task.WaitAsync(TimeSpan.FromSeconds(10));
        PollerCount(orchestrator).Should().Be(1);

        await orchestrator.UpdateRunAsync(runId, "facility", Guid.NewGuid().ToString());
        created.Should().Be(2);
        PollerCount(orchestrator).Should().Be(1);

        releaseShutdown.TrySetResult();
        await stopping.WaitAsync(TimeSpan.FromSeconds(10));

        created.Should().Be(2);
        disposed.Should().Be(2);
        PollerCount(orchestrator).Should().Be(0);
    }

    [Fact]
    public async Task StopAllPollersAsync_disposes_pollers_when_the_host_token_is_already_cancelled()
    {
        var runId = Guid.NewGuid();
        var disposed = 0;
        var store = new Mock<ISnapshotStore>();
        store.Setup(s => s.RegisterRunAsync(It.IsAny<Guid>(), It.IsAny<RunSnapshotMeta>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.SetDomainAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<It.IsAnyType>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.AppendLogsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.GetRunMetaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RunSnapshotMeta?)null);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.Dispose()).Callback(() => Interlocked.Increment(ref disposed));
        var orchestrator = CreateOrchestrator(store, scope);
        await orchestrator.RegisterRunAsync(runId, "facility", Guid.NewGuid().ToString());

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var stopAll = typeof(RunSnapshotOrchestrator).GetMethod(
            "StopAllPollersAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var stopping = (Task)stopAll.Invoke(orchestrator, new object[] { cancelled.Token })!;
        await stopping.WaitAsync(TimeSpan.FromSeconds(10));

        disposed.Should().Be(1);
        PollerCount(orchestrator).Should().Be(0);
    }

    [Fact]
    public async Task StopAllPollersAsync_disposes_the_poller_after_a_domain_write_releases_the_gate()
    {
        var runId = Guid.NewGuid();
        var disposed = 0;
        var insideWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var store = new Mock<ISnapshotStore>();
        store.Setup(s => s.RegisterRunAsync(It.IsAny<Guid>(), It.IsAny<RunSnapshotMeta>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.AppendLogsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.GetRunMetaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunSnapshotMeta
            {
                RunId = runId,
                FacilityId = "other",
                ReportId = "other",
                IsActive = true
            });
        store.Setup(s => s.SetDomainAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<It.IsAnyType>(), It.IsAny<CancellationToken>()))
            .Returns(new InvocationFunc(_ =>
            {
                insideWrite.TrySetResult();
                return releaseWrite.Task;
            }));

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.Dispose()).Callback(() => Interlocked.Increment(ref disposed));
        var orchestrator = CreateOrchestrator(store, scope);
        await orchestrator.RegisterRunAsync(runId, "facility", Guid.NewGuid().ToString());

        var writing = orchestrator.WriteDomainAsync(runId, "generationManifest", "during-shutdown", CancellationToken.None);
        await insideWrite.Task.WaitAsync(TimeSpan.FromSeconds(10));

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var stopAll = typeof(RunSnapshotOrchestrator).GetMethod(
            "StopAllPollersAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var stopping = (Task)stopAll.Invoke(orchestrator, new object[] { cancelled.Token })!;

        await Task.WhenAny(stopping, Task.Delay(500));
        stopping.IsCompleted.Should().BeFalse("shutdown waits for the domain write to release the gate");
        disposed.Should().Be(0);

        releaseWrite.TrySetResult();
        await writing.WaitAsync(TimeSpan.FromSeconds(10));
        await stopping.WaitAsync(TimeSpan.FromSeconds(10));

        disposed.Should().Be(1);
        PollerCount(orchestrator).Should().Be(0);
    }

    [Fact]
    public async Task QuiesceForDeleteAsync_stops_the_poller_and_a_later_domain_write_does_not_land()
    {
        var runId = Guid.NewGuid();
        var writes = 0;
        var metaReads = 0;
        var runDeleted = 0;
        var insideWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        RunSnapshotMeta? meta = new()
        {
            RunId = runId,
            FacilityId = "other",
            ReportId = "other",
            IsActive = true
        };

        var store = new Mock<ISnapshotStore>();
        store.Setup(s => s.RegisterRunAsync(It.IsAny<Guid>(), It.IsAny<RunSnapshotMeta>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.AppendLogsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.CompleteRunAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.GetRunMetaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                Interlocked.Increment(ref metaReads);
                return Task.FromResult(meta);
            });
        store.Setup(s => s.SetDomainAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<It.IsAnyType>(), It.IsAny<CancellationToken>()))
            .Returns(new InvocationFunc(invocation =>
            {
                var domain = (string)invocation.Arguments[1];
                return OnWriteAsync(domain);
            }));

        var orchestrator = CreateOrchestrator(store);
        await orchestrator.RegisterRunAsync(runId, "facility", Guid.NewGuid().ToString());

        var writing = orchestrator.WriteDomainAsync(runId, "generationManifest", "during-cancel", CancellationToken.None);
        await insideWrite.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var quiescing = orchestrator.QuiesceForDeleteAsync(runId, _ =>
        {
            meta = null;
            Interlocked.Increment(ref runDeleted);
            return Task.CompletedTask;
        });
        await Task.Delay(200);
        quiescing.IsCompleted.Should().BeFalse("an in-flight domain write holds the run gate");

        releaseWrite.TrySetResult();
        await writing.WaitAsync(TimeSpan.FromSeconds(10));
        await quiescing.WaitAsync(TimeSpan.FromSeconds(10));

        writes.Should().Be(1);
        runDeleted.Should().Be(1);
        PollerCount(orchestrator).Should().Be(0);
        var metaReadsAfterDelete = Volatile.Read(ref metaReads);

        await orchestrator.WriteDomainAsync(runId, "generationManifest", "after-delete", CancellationToken.None);
        await orchestrator.CompleteRunAsync(runId);
        await Task.Delay(300);

        writes.Should().Be(1);
        Volatile.Read(ref metaReads).Should().BeGreaterThan(metaReadsAfterDelete);
        PollerCount(orchestrator).Should().Be(0);
        GateCount(orchestrator).Should().Be(0);

        async Task OnWriteAsync(string domain)
        {
            if (domain == "generationManifest")
            {
                insideWrite.TrySetResult();
                await releaseWrite.Task;
            }

            Interlocked.Increment(ref writes);
        }
    }

    private static int GateCount(RunSnapshotOrchestrator orchestrator)
    {
        var gates = typeof(RunSnapshotOrchestrator)
            .GetField("_runGates", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(orchestrator)!;
        return (int)gates.GetType().GetProperty("Count")!.GetValue(gates)!;
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

    private static RunSnapshotOrchestrator CreateOrchestrator(
        Mock<ISnapshotStore> store,
        Mock<IServiceScope>? scope = null,
        Action? onScopeCreated = null)
    {
        var reader = new PipelineDataReader(
            Mock.Of<IReportServiceClient>(),
            Mock.Of<IDataAcquisitionServiceClient>(),
            Mock.Of<INormalizationServiceClient>(),
            Mock.Of<IFacilityServiceClient>());
        var scopeProvider = new Mock<IServiceProvider>();
        scopeProvider.Setup(provider => provider.GetService(typeof(PipelineDataReader))).Returns(reader);
        scope ??= new Mock<IServiceScope>();
        scope.SetupGet(s => s.ServiceProvider).Returns(scopeProvider.Object);
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(factory => factory.CreateScope()).Returns(() =>
        {
            onScopeCreated?.Invoke();
            return scope.Object;
        });
        var services = new Mock<IServiceProvider>();
        services.Setup(provider => provider.GetService(typeof(IServiceScopeFactory))).Returns(scopeFactory.Object);
        return new RunSnapshotOrchestrator(store.Object, services.Object, NullLogger<RunSnapshotOrchestrator>.Instance);
    }
}
