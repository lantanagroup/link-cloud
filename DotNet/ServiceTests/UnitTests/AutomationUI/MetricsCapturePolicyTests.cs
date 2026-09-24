using Automation.UI.Services;
using FluentAssertions;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class MetricsCapturePolicyTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void Capture_only_when_metrics_run_and_validators_passed(
        bool isMetricsRun, bool validatorsPassed, bool expected)
    {
        MetricsCapturePolicy.ShouldCapture(isMetricsRun, validatorsPassed).Should().Be(expected);
    }
}
