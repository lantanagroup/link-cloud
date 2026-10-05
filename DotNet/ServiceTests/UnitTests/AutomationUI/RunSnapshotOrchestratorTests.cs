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
}
