using System.Collections.Concurrent;
using Automation.UI.Services;
using FluentAssertions;

namespace UnitTests.AutomationUI;

public class PollerSlotTests
{
    [Fact]
    public void Beginning_finalizing_blocks_detach()
    {
        var slot = new PollerSlot();

        slot.TryBeginFinalizing().Should().BeTrue();
        slot.IsFinalizing.Should().BeTrue();
        slot.TryDetach().Should().BeFalse();
        slot.TryBeginFinalizing().Should().BeFalse();
    }

    [Fact]
    public void Detach_blocks_finalizing()
    {
        var slot = new PollerSlot();

        slot.TryDetach().Should().BeTrue();
        slot.TryBeginFinalizing().Should().BeFalse();
        slot.IsFinalizing.Should().BeFalse();
        slot.TryDetach().Should().BeFalse();
    }

    [Fact]
    public void TryRemoveExact_leaves_a_replacement_handle()
    {
        var pollers = new ConcurrentDictionary<Guid, object>();
        var runId = Guid.NewGuid();
        var first = new object();
        var replacement = new object();
        pollers[runId] = first;
        pollers[runId] = replacement;

        RunSnapshotOrchestrator.TryRemoveExact(pollers, runId, first).Should().BeFalse();
        pollers[runId].Should().BeSameAs(replacement);

        RunSnapshotOrchestrator.TryRemoveExact(pollers, runId, replacement).Should().BeTrue();
        pollers.ContainsKey(runId).Should().BeFalse();
    }
}
