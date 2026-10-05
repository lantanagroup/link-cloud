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
}
