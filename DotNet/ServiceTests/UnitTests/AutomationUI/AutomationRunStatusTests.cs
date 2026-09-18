using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Models;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class AutomationRunStatusTests
{
    [Theory]
    [InlineData(AutomationRunStatus.Queued, true)]
    [InlineData(AutomationRunStatus.Running, true)]
    [InlineData(AutomationRunStatus.LiveWindowOpen, true)]
    [InlineData(AutomationRunStatus.ReportFinalization, true)]
    [InlineData(AutomationRunStatus.Cancelled, true)]
    [InlineData(AutomationRunStatus.Succeeded, false)]
    [InlineData(AutomationRunStatus.Failed, false)]
    public void Cancel_is_success_for_in_flight_and_already_cancelled_runs(
        AutomationRunStatus status,
        bool expected)
    {
        status.IsSuccessfulCancelTarget().Should().Be(expected);
    }
}
