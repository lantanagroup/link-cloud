using FluentAssertions;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class InteractiveAvailabilityTests
{
    private const string Facility = "fac-1";
    private static readonly string Report = Guid.Parse("08ea535d-084d-41f9-90cc-6d9c730ae5ec").ToString();

    [Fact]
    public void A_run_whose_report_was_cleaned_up_is_blocked_with_the_reason()
    {
        InteractiveAvailability.ForRun(true, Facility, Report, reportExists: false)
            .Should().Be(InteractiveAvailability.CleanedUp);
    }

    [Fact]
    public void A_run_whose_report_still_exists_can_load_on_demand()
    {
        InteractiveAvailability.ForRun(true, Facility, Report, reportExists: true).Should().BeNull();
    }

    [Fact]
    public void An_unanswered_check_leaves_the_button_on_so_the_load_can_explain()
    {
        InteractiveAvailability.ForRun(true, Facility, Report, reportExists: null).Should().BeNull();
    }

    [Theory]
    [InlineData(null, "08ea535d-084d-41f9-90cc-6d9c730ae5ec")]
    [InlineData("fac-1", null)]
    [InlineData("fac-1", "not-a-guid")]
    [InlineData(" ", "08ea535d-084d-41f9-90cc-6d9c730ae5ec")]
    public void A_run_without_a_report_is_blocked_without_asking_anything(string? facility, string? report)
    {
        InteractiveAvailability.NeedsCheck(true, facility, report).Should().BeFalse();
        InteractiveAvailability.ForRun(true, facility, report, null).Should().Be(InteractiveAvailability.NoReport);
    }

    [Fact]
    public void No_submission_store_blocks_without_asking_anything()
    {
        InteractiveAvailability.NeedsCheck(false, Facility, Report).Should().BeFalse();
        InteractiveAvailability.ForRun(false, Facility, Report, true).Should().Be(InteractiveAvailability.NotConfigured);
    }

    [Fact]
    public void Only_a_run_with_a_report_and_a_store_needs_the_one_existence_check()
    {
        InteractiveAvailability.NeedsCheck(true, Facility, Report).Should().BeTrue();
    }

    [Fact]
    public void The_generation_manifest_decides_with_one_schedule_read_and_points_the_graph_at_the_runs_report()
    {
        var controller = File.ReadAllText(RepoFile("DotNet/Link.UI/Controllers/AutomationController.RunDepth.cs"));
        controller.Should().Contain("await ApplyInteractiveAsync(model, run, cancellationToken);");
        controller.Should().Contain("reports.ReportExistsAsync(run.FacilityId, run.ReportId, cancellationToken)");
        controller.Should().Contain("model.GraphReportId = run.ReportId!.Trim();");

        var service = File.ReadAllText(RepoFile("DotNet/Link.UI/Services/ReportsService.cs"));
        var start = service.IndexOf("public async Task<bool?> ReportExistsAsync(", StringComparison.Ordinal);
        var end = service.IndexOf("public async Task<ReportScheduleApiModel?> TryScheduleAsync(", StringComparison.Ordinal);
        start.Should().BeGreaterThan(0);
        var body = service[start..end];
        body.Should().Contain("_reports.GetScheduleAsync(");
        body.Should().NotContain("CountsAsync");
        body.Should().NotContain("_facilities");
        body.Should().NotContain("OpenReportAsync");
    }

    [Fact]
    public void The_shared_manifest_builds_the_graph_url_from_the_runs_report_and_shows_failures_inline()
    {
        var view = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Shared/_ReportManifest.cshtml"));
        view.Should().Contain("Uri.EscapeDataString(Model.GraphFacilityId)");
        view.Should().Contain("id=\"manifest-interactive-note\"");

        var script = File.ReadAllText(RepoFile("DotNet/Link.UI/wwwroot/js/resource-graph.js"));
        script.Should().Contain("function inlineNote(text)");
        script.Should().Contain("bar.classList.toggle(\"d-none\", !shown)");
        script.Should().NotContain("if (!row || !graphUrl) return;");
        script.Should().Contain("inlineNote(reason);");
    }

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "link-cloud.sln")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
    }
}
