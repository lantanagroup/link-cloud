using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Helpers;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class DataAcquisitionPagingActivityTests
{
    [Fact]
    public void Summarize_returns_null_when_no_paging_lines()
    {
        DataAcquisitionPagingActivity.Summarize(
            ["unrelated healthcheck", "MeasureEval Consuming"],
            TimeSpan.FromSeconds(60)).Should().BeNull();
    }

    [Fact]
    public void Summarize_starting_line_includes_log_and_resource_type()
    {
        var summary = DataAcquisitionPagingActivity.Summarize(
            ["Log 935585 retrieving paged results: starting ServiceRequest search"],
            TimeSpan.FromSeconds(60));

        summary.Should().Be("paging ServiceRequest log 935585 starting (1 log lines/60s)");
    }

    [Fact]
    public void Summarize_page_line_includes_page_and_cumulative()
    {
        var summary = DataAcquisitionPagingActivity.Summarize(
            [
                "Log 935585 retrieving paged results: starting ServiceRequest search",
                "Log 935585 retrieving paged results: ServiceRequest page 3 (100 resources this page, 300 total so far, fetching next page)"
            ],
            TimeSpan.FromSeconds(60));

        summary.Should().Be("paging ServiceRequest log 935585 page 3 (300 total so far, 2 log lines/60s)");
    }

    [Fact]
    public void Summarize_ignores_non_letter_resource_types()
    {
        var summary = DataAcquisitionPagingActivity.Summarize(
            ["Log 1 retrieving paged results: starting Observation\ninjected search"],
            TimeSpan.FromSeconds(60));

        summary.Should().NotBeNull();
        summary.Should().NotContain("injected");
        summary.Should().NotContain("Observation\ninjected");
    }

    [Fact]
    public void Summarize_multiple_logs_lists_ids_and_max_page()
    {
        var summary = DataAcquisitionPagingActivity.Summarize(
            [
                "Log 100 retrieving paged results: Observation page 12 (100 resources this page, 1200 total so far, last page)",
                "Log 200 retrieving paged results: ServiceRequest page 2 (50 resources this page, 150 total so far, fetching next page)"
            ],
            TimeSpan.FromSeconds(60));

        summary.Should().Contain("log");
        summary.Should().Contain("100");
        summary.Should().Contain("200");
        summary.Should().Contain("page 12");
        summary.Should().Contain("1200 total so far");
        summary.Should().Contain("2 log lines/60s");
    }
}
