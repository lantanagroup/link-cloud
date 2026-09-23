using Automation.UI.Services;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class FacilitySetupGateTests
{
    [Fact]
    public async Task Second_run_for_the_same_facility_waits_until_the_first_releases()
    {
        var facilityId = Guid.NewGuid().ToString();
        var entered = 0;
        var firstHolding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = Task.Run(async () =>
        {
            using var held = await FacilitySetupGate.AcquireAsync(facilityId, _ => { }, CancellationToken.None);
            Interlocked.Increment(ref entered);
            firstHolding.SetResult();
            await releaseFirst.Task;
        });

        await firstHolding.Task;
        var second = Task.Run(async () =>
        {
            using var held = await FacilitySetupGate.AcquireAsync(facilityId, _ => { }, CancellationToken.None);
            Interlocked.Increment(ref entered);
        });

        await Task.Delay(100);
        Assert.Equal(1, Volatile.Read(ref entered));
        Assert.False(second.IsCompleted);

        releaseFirst.SetResult();
        await Task.WhenAll(first, second);
        Assert.Equal(2, Volatile.Read(ref entered));
    }

    [Fact]
    public async Task Different_facilities_do_not_wait_on_each_other()
    {
        using var first = await FacilitySetupGate.AcquireAsync(Guid.NewGuid().ToString(), _ => { }, CancellationToken.None);
        using var second = await FacilitySetupGate.AcquireAsync(Guid.NewGuid().ToString(), _ => { }, CancellationToken.None);
    }
}
