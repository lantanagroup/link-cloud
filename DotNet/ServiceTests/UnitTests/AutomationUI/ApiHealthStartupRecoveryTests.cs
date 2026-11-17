using Automation.UI.Models.ApiHealth;
using Automation.UI.Services;
using Automation.UI.Services.ApiHealth;
using Automation.UI.Services.Persistence;
using Automation.UI.Services.TestRail;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class ApiHealthStartupRecoveryTests
{
    private const string Interrupted =
        "API Health run was interrupted by application restart and has been cancelled.";

    [Fact]
    public async Task Interrupted_run_publishes_one_cancelled_result()
    {
        var runId = Guid.NewGuid();
        var startedAt = DateTimeOffset.Parse("2026-11-06T12:00:00Z");
        ApiHealthTestRailPublishRequest? published = null;
        var publisher = new Mock<ITestRailPublisher>();
        publisher
            .Setup(p => p.PublishApiHealthRunAsync(It.IsAny<ApiHealthTestRailPublishRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ApiHealthTestRailPublishRequest, CancellationToken>((request, _) => published = request)
            .Returns(() => System.Threading.Tasks.Task.FromResult<IReadOnlyList<TestRailRunReference>>([]));

        var store = new Mock<IApiHealthRunStore>();
        store.Setup(s => s.GetActiveExecutionRunStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiHealthExecutionRunStatus
            {
                RunId = runId,
                Scope = "Service",
                ServiceName = "Tenant",
                StartedAt = startedAt
            });

        var service = new ApiHealthStartupRecoveryService(
            store.Object,
            Mock.Of<IAutomationRunManager>(),
            Mock.Of<ISnapshotStore>(),
            publisher.Object,
            NullLogger<ApiHealthStartupRecoveryService>.Instance);

        await service.StartAsync(CancellationToken.None);

        published.Should().NotBeNull();
        published!.RunId.Should().Be(runId);
        published.Scope.Should().Be("Service");
        published.ServiceName.Should().Be("Tenant");
        published.Cancelled.Should().BeTrue();
        published.Failed.Should().BeTrue();
        published.Error.Should().Be(Interrupted);
        published.StartedAt.Should().Be(startedAt);
        published.FinishedAt.Should().NotBeNull();
        publisher.Verify(
            p => p.PublishApiHealthRunAsync(It.IsAny<ApiHealthTestRailPublishRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
        store.Verify(
            s => s.CompleteExecutionRunAsync(runId, true, Interrupted, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Interrupted_run_with_a_seed_still_publishes_api_health_once()
    {
        var runId = Guid.NewGuid();
        var seedRunId = Guid.NewGuid();
        var publisher = new Mock<ITestRailPublisher>();
        publisher
            .Setup(p => p.PublishApiHealthRunAsync(It.IsAny<ApiHealthTestRailPublishRequest>(), It.IsAny<CancellationToken>()))
            .Returns(() => System.Threading.Tasks.Task.FromResult<IReadOnlyList<TestRailRunReference>>([]));

        var store = new Mock<IApiHealthRunStore>();
        store.Setup(s => s.GetActiveExecutionRunStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiHealthExecutionRunStatus
            {
                RunId = runId,
                Scope = "All",
                StartedAt = DateTimeOffset.UtcNow,
                SeedRunId = seedRunId
            });

        var runs = new Mock<IAutomationRunManager>();
        runs.SetupSequence(r => r.GetRunAsync(seedRunId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutomationRunSummary { RunId = seedRunId, Status = AutomationRunStatus.Running })
            .ReturnsAsync(new AutomationRunSummary { RunId = seedRunId, Status = AutomationRunStatus.Cancelled });
        runs.Setup(r => r.CancelRunAsync(seedRunId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new ApiHealthStartupRecoveryService(
            store.Object,
            runs.Object,
            Mock.Of<ISnapshotStore>(),
            publisher.Object,
            NullLogger<ApiHealthStartupRecoveryService>.Instance);

        await service.StartAsync(CancellationToken.None);

        runs.Verify(r => r.CancelRunAsync(seedRunId, It.IsAny<CancellationToken>()), Times.Once);
        publisher.Verify(
            p => p.PublishApiHealthRunAsync(
                It.Is<ApiHealthTestRailPublishRequest>(request => request.RunId == runId && request.Cancelled),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task No_active_run_does_not_publish()
    {
        var publisher = new Mock<ITestRailPublisher>(MockBehavior.Strict);
        var store = new Mock<IApiHealthRunStore>();
        store.Setup(s => s.GetActiveExecutionRunStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApiHealthExecutionRunStatus?)null);

        var service = new ApiHealthStartupRecoveryService(
            store.Object,
            Mock.Of<IAutomationRunManager>(),
            Mock.Of<ISnapshotStore>(),
            publisher.Object,
            NullLogger<ApiHealthStartupRecoveryService>.Instance);

        await service.StartAsync(CancellationToken.None);

        publisher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Publish_failure_does_not_fail_startup()
    {
        var store = new Mock<IApiHealthRunStore>();
        store.Setup(s => s.GetActiveExecutionRunStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiHealthExecutionRunStatus
            {
                RunId = Guid.NewGuid(),
                Scope = "Service",
                ServiceName = "Tenant",
                StartedAt = DateTimeOffset.UtcNow
            });
        var publisher = new Mock<ITestRailPublisher>();
        publisher
            .Setup(p => p.PublishApiHealthRunAsync(It.IsAny<ApiHealthTestRailPublishRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("testrail down"));

        var service = new ApiHealthStartupRecoveryService(
            store.Object,
            Mock.Of<IAutomationRunManager>(),
            Mock.Of<ISnapshotStore>(),
            publisher.Object,
            NullLogger<ApiHealthStartupRecoveryService>.Instance);

        var act = () => service.StartAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        store.Verify(
            s => s.CompleteExecutionRunAsync(
                It.IsAny<Guid>(),
                true,
                Interrupted,
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
