using Automation.UI.Services;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class AutomationRunManagerLogPublishTests
{
    [Fact]
    public async Task Cancelled_run_with_trimmed_memory_log_publishes_the_persisted_log()
    {
        var runId = Guid.NewGuid();
        var state = NewState(runId);
        state.Status = AutomationRunStatus.Cancelled;
        state.CancelRequested = true;
        state.RunCancellation.Cancel();
        state.Logs.Add("tail-only");
        state.LogTrimmed = true;

        var persisted = new List<string> { "early-line", "tail-only" };
        var store = new Mock<ISnapshotStore>();
        store.Setup(s => s.GetLogsAsync(runId, It.IsAny<CancellationToken>()))
            .Returns<Guid, CancellationToken>((_, token) =>
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult(persisted);
            });

        var read = await AutomationRunManager.ReadLogsForPublishAsync(
            state,
            store.Object,
            NullLogger<AutomationRunManager>.Instance);

        read.Lines.Should().Equal(persisted);
        read.MayBeIncomplete.Should().BeFalse();
        store.Verify(
            s => s.GetLogsAsync(runId, It.Is<CancellationToken>(token => !token.IsCancellationRequested)),
            Times.Once);
    }

    [Fact]
    public async Task Trimmed_log_with_failed_appends_is_not_called_complete()
    {
        var runId = Guid.NewGuid();
        var state = NewState(runId);
        state.Status = AutomationRunStatus.Failed;
        state.Logs.Add("tail-only");
        state.LogTrimmed = true;
        state.FailedLogAppends = 1;

        var persisted = new List<string> { "early-line", "tail-only" };
        var store = new Mock<ISnapshotStore>();
        store.Setup(s => s.GetLogsAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(persisted);

        var read = await AutomationRunManager.ReadLogsForPublishAsync(
            state,
            store.Object,
            NullLogger<AutomationRunManager>.Instance);

        read.Lines.Should().Equal(persisted);
        read.MayBeIncomplete.Should().BeTrue();
    }

    private static MutableRunState NewState(Guid runId) =>
        new(
            runId,
            scenarioId: null,
            AutomationScenarioKind.AdhocReportTest,
            new ResolvedRunOptions(1, 1, 1, 1, 1, 1, false, true, [], [], []),
            runNameOverride: null,
            runConfigurationJson: null);
}
