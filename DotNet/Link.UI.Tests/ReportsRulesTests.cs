using FluentAssertions;
using Link.UI.Models;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class ReportsRulesTests
{
    [Fact]
    public void Download_requires_a_package_and_a_report_that_is_not_deleted()
    {
        ReportsRules.CanDownload(deleted: false, payloadRootUri: "reports/one").Should().BeTrue();
        ReportsRules.CanDownload(deleted: true, payloadRootUri: "reports/one").Should().BeFalse();
        ReportsRules.CanDownload(deleted: false, payloadRootUri: "  ").Should().BeFalse();
    }

    [Fact]
    public void Daily_and_monthly_periods_are_calendar_ranges_in_utc()
    {
        ReportsRules.TryReportingPeriod("Daily", new DateOnly(2026, 3, 15), null, "UTC", out var start, out var end, out var error)
            .Should().BeTrue();
        error.Should().BeNull();
        start.Should().Be(new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2026, 3, 15, 23, 59, 59, DateTimeKind.Utc));

        ReportsRules.TryReportingPeriod("Monthly", new DateOnly(2026, 3, 2), null, null, out start, out end, out error)
            .Should().BeTrue();
        end.Should().Be(new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc));
    }

    [Fact]
    public void Custom_period_requires_an_end_and_an_unknown_zone_is_an_error()
    {
        ReportsRules.TryReportingPeriod("Custom", new DateOnly(2026, 3, 1), null, "UTC", out _, out _, out var error)
            .Should().BeFalse();
        error.Should().Be("End date is required for a custom period.");

        ReportsRules.TryReportingPeriod("Daily", new DateOnly(2026, 3, 1), null, "Not/AZone", out _, out _, out error)
            .Should().BeFalse();
        error.Should().Contain("not recognized");
    }

    [Fact]
    public void Patient_and_measure_ids_split_and_drop_blanks()
    {
        ReportsRules.SplitIds(" a, a\nb ").Should().Equal("a", "b");
        ReportsRules.ReportTypes(["CAUTI", "CAUTI"], " CLABSI ").Should().Equal("CAUTI", "CLABSI");
    }

    [Fact]
    public void Measure_ids_come_from_a_summary_list()
    {
        var json = """[{"id":"NHSNGlycemicControlHypoglycemiaInitialPopulation","version":1},{"id":"  "}]""";
        ReportsRules.MeasureIds(json).Should().Equal("NHSNGlycemicControlHypoglycemiaInitialPopulation");
        ReportsRules.MeasureIds("not-json").Should().BeEmpty();
    }

    [Fact]
    public void Issues_group_by_category_and_uncategorized_is_unacceptable()
    {
        var issues = new[]
        {
            Issue("p1", new ValidationCategoryRow { Title = "Timing", Acceptable = false, Guidance = "Fix the clock" }),
            Issue("p2", new ValidationCategoryRow { Title = "Timing", Acceptable = false, Guidance = "Fix the clock" }),
            Issue("p3", new ValidationCategoryRow { Title = "Optional", Acceptable = true, Guidance = "Review" }),
            Issue("p4")
        };

        var groups = ReportsRules.GroupIssues(issues);
        groups.Should().Contain(group => group.Name == "Timing" && !group.Acceptable && group.Count == 2 && group.Guidance == "Fix the clock");
        groups.Should().Contain(group => group.Name == "Optional" && group.Acceptable && group.Count == 1);
        groups.Should().Contain(group => group.Name == "Uncategorized" && !group.Acceptable && group.Count == 1);

        var listed = ReportsRules.IssuesInCategory(issues, "Timing", out var total);
        total.Should().Be(2);
        listed.Select(issue => issue.PatientId).Should().Equal("p1", "p2");
    }

    [Fact]
    public void Result_summary_reads_the_count_without_the_issue_bodies()
    {
        ReportsRules.TryParseResultSummary("""{"count":4,"severity":"INFORMATION"}""", out var count, out var severity)
            .Should().BeTrue();
        count.Should().Be(4);
        severity.Should().Be("INFORMATION");
        ReportsRules.TryParseResultSummary("[]", out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Pretty_json_indents_a_small_document_and_refuses_a_huge_one()
    {
        ReportsRules.PrettyJson("""{"a":1}""", out var tooLarge).Should().Contain("\"a\"");
        tooLarge.Should().BeFalse();

        ReportsRules.PrettyJson(new string('x', ReportsRules.MaxJsonChars + 1), out tooLarge).Should().BeNull();
        tooLarge.Should().BeTrue();
    }

    [Fact]
    public void Validation_is_one_tab_and_prequalification_redirects()
    {
        var root = Root();
        File.Exists(Path.Combine(root, "Views", "Reports", "Prequal.cshtml")).Should().BeFalse();
        var nav = File.ReadAllText(Path.Combine(root, "Views", "Shared", "_ReportNav.cshtml"));
        nav.Should().Contain(">Validation</a>");
        nav.Should().NotContain("Prequalification");
        nav.Should().Contain(">Acquisition log</a>");
        var page = File.ReadAllText(Path.Combine(root, "Views", "Reports", "Validation.cshtml"));
        page.Should().Contain("id=\"validation-live\"");
        page.Should().Contain("Download CSV");
        page.Should().Contain("/Reports/ValidationCsv");
        page.Should().NotContain("data-au-filter");
        page.Should().Contain("All categories");
        page.Should().Contain("id=\"validationIssues\"");
        page.Should().Contain("ReportsRules.CategoryDrilldown");
        page.Should().Contain("Reported issues:");
        page.Should().Contain("Available saved issues:");
        page.Should().Contain("No saved issues are available for this category. Reported counts can include findings that were not saved or have been deleted.");
        page.Should().NotContain("Open prequalification");
        page.Should().NotContain("does not download the issue list");
        var controller = File.ReadAllText(Path.Combine(root, "Controllers", "ReportsController.cs"));
        controller.Should().Contain("LocalRedirect");
        controller.Should().Contain("/Reports/Validation");
    }

    [Fact]
    public void Log_page_size_outside_the_allowed_set_uses_the_default()
    {
        ReportsRules.ClampLogPageSize(7).Should().Be(ReportsRules.DefaultLogPageSize);
        ReportsRules.ClampLogPageSize(50).Should().Be(50);
    }

    [Theory]
    [InlineData("PatientIdentified", "Patient Identified")]
    [InlineData("NotEligable", "Not Eligible")]
    [InlineData("", "—")]
    public void Status_labels_match_the_report_page(string status, string expected)
    {
        var label = status is "NotEligable"
            ? ReportsRules.SubmissionLabel(status)
            : ReportsRules.ReportingLabel(status);
        if (status.Length == 0)
            ReportsRules.ReportingLabel(status).Should().Be(expected);
        else
            label.Should().Be(expected);
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, "DotNet", "Link.UI");
    }

    private static ValidationIssueRow Issue(string patient, ValidationCategoryRow? category = null) => new()
    {
        PatientId = patient,
        Message = patient,
        Categories = category is null ? [] : [category]
    };
}
