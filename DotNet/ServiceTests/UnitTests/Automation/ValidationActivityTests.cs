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
}
