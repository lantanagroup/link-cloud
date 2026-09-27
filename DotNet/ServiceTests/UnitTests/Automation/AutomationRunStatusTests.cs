using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Models;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class AutomationRunStatusTests
{
    [Fact]
    public void CollectingMetrics_is_in_progress_not_terminal()
    {
        var status = AutomationRunStatus.CollectingMetrics;
        status.IsTerminal().Should().BeFalse();
        status.IsInProgress().Should().BeTrue();
        status.IsCancellable().Should().BeTrue();
        status.ToDisplayName().Should().Be("Collecting");
    }

    [Fact]
    public void Succeeded_stays_terminal()
    {
        AutomationRunStatus.Succeeded.IsTerminal().Should().BeTrue();
        AutomationRunStatus.Succeeded.IsInProgress().Should().BeFalse();
        AutomationRunStatus.Succeeded.ToDisplayName().Should().Be("Succeeded");
    }
}
