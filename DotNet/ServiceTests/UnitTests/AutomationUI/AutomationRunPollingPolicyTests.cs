using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Helpers;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class AutomationRunPollingPolicyTests
{
    [Fact]
    public void Lightweight_runs_use_15_second_loops_and_skip_full_domain_polls()
    {
        AutomationRunPollingPolicy.OrchestratorInterval(anyMetricsRun: false).Should().Be(TimeSpan.FromSeconds(15));
        AutomationRunPollingPolicy.PollerInterval(isMetricsRun: false).Should().Be(TimeSpan.FromSeconds(15));
        AutomationRunPollingPolicy.DiagnosticsInterval(isMetricsRun: false, patientCount: 10).Should().Be(TimeSpan.FromSeconds(15));
        AutomationRunPollingPolicy.PollAllDomainsDuringRun(false).Should().BeFalse();
        AutomationRunPollingPolicy.ScrapeNormalizationResourceTypes(false).Should().BeFalse();
    }

    [Fact]
    public void Metrics_runs_keep_current_cadence()
    {
        AutomationRunPollingPolicy.OrchestratorInterval(anyMetricsRun: true).Should().Be(TimeSpan.FromSeconds(2));
        AutomationRunPollingPolicy.PollerInterval(isMetricsRun: true).Should().Be(TimeSpan.FromSeconds(5));
        AutomationRunPollingPolicy.DiagnosticsInterval(isMetricsRun: true, patientCount: 10).Should().Be(TimeSpan.FromSeconds(5));
        AutomationRunPollingPolicy.PollAllDomainsDuringRun(true).Should().BeTrue();
        AutomationRunPollingPolicy.ScrapeNormalizationResourceTypes(true).Should().BeTrue();
    }

    [Fact]
    public void Metrics_runs_with_500_plus_patients_keep_15_second_diagnostics()
    {
        AutomationRunPollingPolicy.DiagnosticsInterval(isMetricsRun: true, patientCount: 500).Should().Be(TimeSpan.FromSeconds(15));
        AutomationRunPollingPolicy.DiagnosticsInterval(isMetricsRun: true, patientCount: 499).Should().Be(TimeSpan.FromSeconds(5));
    }
}
