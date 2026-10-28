using Automation.UI.Services;
using FluentAssertions;
using LantanaGroup.Automation.Helpers;
using LantanaGroup.Link.Shared.Application.Services;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class RunExecutorFacilityAbortTests
{
    [Fact]
    public async Task Stale_facility_abort_stays_set_until_this_run_holds_the_facility_gate()
    {
        var facilityId = "nhsn-" + Guid.NewGuid().ToString("N");
        var previousReportId = Guid.NewGuid().ToString();
        var registry = new InMemoryPipelineAbortRegistry();
        await registry.AbortAsync(facilityId, previousReportId, TimeSpan.FromDays(14));

        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstHolding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = Task.Run(async () =>
        {
            using var held = await FacilitySetupGate.AcquireAsync(facilityId, _ => { }, CancellationToken.None);
            firstHolding.TrySetResult();
            await releaseFirst.Task;
        });

        await firstHolding.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var waitingForGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = Task.Run(() => RunExecutor.AcquireFacilityAndClearStaleAbortAsync(
            facilityId,
            registry,
            new NullOutput(),
            message =>
            {
                if (message.Contains("already running", StringComparison.Ordinal))
                    waitingForGate.TrySetResult();
            },
            CancellationToken.None));

        var signaled = await Task.WhenAny(waitingForGate.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(waitingForGate.Task, signaled);

        (await registry.IsAbortedAsync(facilityId, reportId: null)).Should().BeTrue();
        (await registry.IsAbortedAsync(facilityId: null, previousReportId)).Should().BeTrue();

        releaseFirst.TrySetResult();
        using var secondGate = await second.WaitAsync(TimeSpan.FromSeconds(5));
        await first.WaitAsync(TimeSpan.FromSeconds(5));

        (await registry.IsAbortedAsync(facilityId, reportId: null)).Should().BeFalse();
        (await registry.IsAbortedAsync(facilityId: null, previousReportId)).Should().BeTrue();
        secondGate.Should().NotBeNull();
    }

    [Fact]
    public async Task Scheduled_workflow_runs_only_after_the_stale_facility_abort_is_cleared()
    {
        var facilityId = "nhsn-" + Guid.NewGuid().ToString("N");
        var previousReportId = Guid.NewGuid().ToString();
        var registry = new InMemoryPipelineAbortRegistry();
        await registry.AbortAsync(facilityId, previousReportId, TimeSpan.FromDays(14));

        var entered = false;
        var result = await RunExecutor.InvokeAfterClearingStaleFacilityAbortAsync(
            registry,
            facilityId,
            new NullOutput(),
            async ct =>
            {
                entered = true;
                (await registry.IsAbortedAsync(facilityId, reportId: null, ct)).Should().BeFalse();
                (await registry.IsAbortedAsync(facilityId: null, previousReportId, ct)).Should().BeTrue();
                return "scheduled";
            },
            CancellationToken.None);

        entered.Should().BeTrue();
        result.Should().Be("scheduled");
        (await registry.IsAbortedAsync(facilityId, reportId: null)).Should().BeFalse();
        (await registry.IsAbortedAsync(facilityId: null, previousReportId)).Should().BeTrue();
    }

    private sealed class NullOutput : IAutomationOutput
    {
        public void WriteLine(string message) { }
        public void WriteLine(string format, params object[] args) { }
    }
}
