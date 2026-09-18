using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Helpers;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class ValidationActivityTests
{
    [Fact]
    public void Summarize_returns_null_when_no_heartbeat_lines()
    {
        ValidationActivity.Summarize(
            ["Starting validation of Bundle with 12 entries", "Retrieved patient bundle with 12 entries"],
            TimeSpan.FromSeconds(60)).Should().BeNull();
    }

    [Fact]
    public void Summarize_includes_detail_and_max_elapsed()
    {
        var summary = ValidationActivity.Summarize(
            [
                "validation still in progress: FHIR bundle 11781 entries (elapsed 30s)",
                "validation still in progress: FHIR bundle 11781 entries (elapsed 90s)"
            ],
            TimeSpan.FromSeconds(60));

        summary.Should().Be("FHIR bundle 11781 entries, elapsed 90s, 2 log lines/60s");
    }

    [Fact]
    public void Summarize_uses_categorize_detail()
    {
        var summary = ValidationActivity.Summarize(
            ["validation still in progress: categorizing 71117 results (elapsed 45s)"],
            TimeSpan.FromSeconds(60));

        summary.Should().Contain("categorizing 71117 results");
        summary.Should().Contain("elapsed 45s");
    }

    [Fact]
    public void MatchesRun_requires_facility_and_report_when_supplied()
    {
        const string facility = "569e8402-6b79-4954-a04e-9bd04b3bce6e";
        const string report = "9aa1c3dd-04f8-4055-af00-a36c42db0ff3";
        var line = $"validation still in progress: FHIR bundle 11781 entries facility={facility} report={report} (elapsed 90s)";

        ValidationActivity.MatchesRun(line, facility, report).Should().BeTrue();
        ValidationActivity.MatchesRun(line, Guid.NewGuid().ToString(), report).Should().BeFalse();
        ValidationActivity.MatchesRun(line, facility, Guid.NewGuid().ToString()).Should().BeFalse();
    }

    [Fact]
    public void Processing_line_with_facility_and_report_matches_run()
    {
        const string facility = "569e8402-6b79-4954-a04e-9bd04b3bce6e";
        const string report = "9aa1c3dd-04f8-4055-af00-a36c42db0ff3";
        var line = $"Processing ReadyForValidation message for facility {facility}, patient Patient-af75b673-001, report {report}";

        ValidationActivity.MatchesRun(line, facility, report).Should().BeTrue();
        ValidationActivity.Summarize([line], TimeSpan.FromSeconds(60)).Should().BeNull();
    }
}
