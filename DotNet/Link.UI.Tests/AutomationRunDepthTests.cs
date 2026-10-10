using Automation.UI.Services;
using FluentAssertions;
using Xunit;

namespace Link.UI.Tests;

public class AutomationRunDepthTests
{
    [Fact]
    public void Log_page_zero_is_the_last_page_and_order_follows_sequence_then_ordinal()
    {
        var lines = new RunLogPaging.SourceLine[]
        {
            new(3, 0, "chunk", 0),
            new(1, 1, "legacy", 1),
            new(1, 0, "legacy", 0),
            new(2, 0, "chunk", 1)
        };

        var last = RunLogPaging.Select(lines, 0, 2);
        last.PageNumber.Should().Be(2);
        last.TotalPages.Should().Be(2);
        last.Items.Select(item => item.Sequence).Should().Equal(2, 3);

        var first = RunLogPaging.Select(lines, 1, 2);
        first.Items.Select(item => (item.Sequence, item.Ordinal)).Should().Equal((1L, 0), (1L, 1));
    }

    [Fact]
    public void Log_page_past_the_end_clamps_and_an_empty_log_is_one_page()
    {
        RunLogPaging.Select([], 4, 10).PageNumber.Should().Be(1);
        RunLogPaging.Select([], 4, 10).Items.Should().BeEmpty();
        RunLogPaging.Select([new RunLogPaging.SourceLine(1, 0, "a", 0)], 9, 10).PageNumber.Should().Be(1);
        RunLogPaging.NormalizePageSize(0).Should().Be(RunLogPaging.DefaultPageSize);
        RunLogPaging.NormalizePageSize(5000).Should().Be(RunLogPaging.MaxPageSize);
    }

    [Fact]
    public void Dashboard_and_run_views_keep_the_ported_controls()
    {
        var index = Read("DotNet/Link.UI/Views/Automation/Index.cshtml");
        index.Should().Contain("Quick Launch");
        index.Should().Contain("id=\"activeCard\"");
        index.Should().NotContain("bg-primary");
        index.Should().Contain("id=\"recentRunsHost\"");
        index.Should().Contain("id=\"btnNewScenarioFromRuns\"");
        index.Should().Contain("name=\"choice\"");

        var recent = Read("DotNet/Link.UI/Views/Automation/_RecentRuns.cshtml");
        recent.Should().Contain("Seed");
        recent.Should().Contain("Duration");
        recent.Should().Contain("Finished");
        recent.Should().Contain("data-au-table");
        recent.Should().Contain("id=\"pageSizeSelect\"");
        recent.Should().Contain("btn-cancel-run");
        recent.Should().Contain("btn-delete-run");

        var dashboard = Read("DotNet/Link.UI/wwwroot/js/automation-dashboard.js");
        dashboard.Should().Contain("/Automation/recent");
        dashboard.Should().Contain("window.luPaintTimes(host)");
        dashboard.Should().Contain("btn-cancel-run");
        dashboard.Should().Contain("btn-delete-run");
        dashboard.Should().Contain("btnNewScenarioFromRuns");

        var detail = Read("DotNet/Link.UI/Views/Automation/_RunDetail.cshtml");
        detail.Should().NotContain("Model.Logs");
        detail.Should().Contain("id=\"logBox\"");
        detail.Should().Contain("!Model.Status.IsTerminal()");
        detail.Should().Contain("name=\"_ServiceMonitor\"");
        detail.Should().NotContain("id=\"liveUtilizationCard\"");
        detail.Should().Contain("id=\"btnCancelRun\"");

        var script = Read("DotNet/Link.UI/Views/Automation/_RunDetailScript.cshtml");
        script.Should().Contain("Logs\", \"Automation\"");
        script.Should().Contain("payload.run");
        script.Should().Contain("refreshStoredLogs");
        script.Should().Contain("CancelJson");
        script.Should().Contain("DeleteJson");
        script.Should().Contain("Export");
        script.Should().Contain("window.auPulseStop");
        script.Should().Contain("window.luPaintTimes(host)");

        var monitor = Read("DotNet/Link.UI/Views/Shared/_ServiceMonitor.cshtml");
        monitor.Should().Contain("id=\"liveUtilizationCard\"");
        monitor.Should().Contain("data-url=");
        Read("DotNet/Link.UI/Views/Metrics/Index.cshtml").Should().Contain("name=\"_ServiceMonitor\"");
        Read("DotNet/Link.UI/Views/Automation/Run.cshtml").Should().Contain("!detail.Status.IsTerminal()");
        var pulse = Read("DotNet/Link.UI/wwwroot/js/live-utilization.js");
        pulse.Should().Contain("window.auPulseStop");
        pulse.Should().Contain("if (!card || !card.getAttribute('data-url') || timer) return;");

        var manifest = Read("DotNet/Link.UI/Views/Automation/Manifest.cshtml");
        manifest.Should().Contain("_ReportManifest");
        manifest.Should().Contain("\"Automation\"");
        manifest.Should().Contain("asp-action=\"Run\"");
        var shared = Read("DotNet/Link.UI/Views/Shared/_ReportManifest.cshtml");
        shared.Should().Contain("Generated vs ABS");
        shared.Should().Contain("ShowComparison");
        var reports = Read("DotNet/Link.UI/Views/Reports/Manifest.cshtml");
        reports.Should().Contain("_ReportManifest");
        reports.Should().NotContain("Generated vs ABS");

        var runJs = Read("DotNet/Link.UI/wwwroot/js/automation-run.js");
        runJs.Should().Contain("getElementById(\"runStatus\") || document.getElementById(\"status\")");
        runJs.Should().Contain("refreshStoredLogs");
    }

    [Fact]
    public void Shared_surfaces_reuse_one_partial_one_pill_and_drop_the_dead_log_routes()
    {
        var detail = Read("DotNet/Link.UI/Views/Automation/_RunDetail.cshtml");
        detail.Should().Contain("~/Views/Shared/_AdvancedPerformance.cshtml");
        detail.Should().Contain("asp-controller=\"Reports\"");
        detail.Should().Contain("asp-action=\"Validation\"");
        detail.Should().Contain("asp-route-reportId=\"@Model.ReportId\"");
        Read("DotNet/Link.UI/Views/Metrics/Details.cshtml").Should().Contain("PartialAsync(\"_AdvancedPerformance\"");
        Read("DotNet/Link.UI/Views/Shared/_AdvancedPerformance.cshtml").Should().Contain("Model.PreviousRunHref");

        var root = RepoRoot();
        File.Exists(Path.Combine(root, "DotNet", "Link.UI", "Views", "Automation", "_AdvancedPerformance.cshtml")).Should().BeFalse();
        File.Exists(Path.Combine(root, "DotNet", "Link.UI", "Views", "Metrics", "_AdvancedPerformance.cshtml")).Should().BeFalse();

        var normalization = Read("DotNet/Link.UI/Views/Normalizations/Index.cshtml");
        normalization.Should().Contain("FacilityNormalizationRules.Label");
        normalization.Should().Contain("FacilityNormalizationRules.HslocDescription");
        normalization.Should().Contain("FacilityNormalizationRules.CopyLocationDescription");
        normalization.Should().Contain("FacilityNormalizationRules.CopyAliasDescription");
        normalization.Should().Contain("function operationLabel");

        Read("DotNet/Link.UI/Views/System/Health.cshtml").Should().Contain("asp-controller=\"ApiHealth\"");
        Read("DotNet/Link.UI/Views/ApiHealth/Index.cshtml").Should().Contain("asp-action=\"Health\"");

        var recent = Read("DotNet/Link.UI/Views/Automation/_RecentRuns.cshtml");
        recent.Should().Contain("StatusPills.ForRun");
        recent.Should().NotContain("run-status-badge");
        Read("DotNet/Link.UI/Views/Reports/Index.cshtml").Should().Contain("StatusPills.ForSchedule");

        var depth = Read("DotNet/Link.UI/Controllers/AutomationController.RunDepth.cs");
        depth.Should().NotContain("acquisition-logs");
        depth.Should().NotContain("acquisition-log");
        depth.Should().Contain("PreviousRunHref");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;

        dir.Should().NotBeNull();
        return dir!.FullName;
    }

    private static string Read(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;

        dir.Should().NotBeNull();
        return File.ReadAllText(Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar)));
    }
}
