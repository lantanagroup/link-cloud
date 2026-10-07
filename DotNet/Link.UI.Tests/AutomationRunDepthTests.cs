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
        index.Should().Contain("card-header bg-primary text-white");
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
        dashboard.Should().Contain("btn-cancel-run");
        dashboard.Should().Contain("btn-delete-run");
        dashboard.Should().Contain("btnNewScenarioFromRuns");

        var detail = Read("DotNet/Link.UI/Views/Automation/_RunDetail.cshtml");
        detail.Should().NotContain("Model.Logs");
        detail.Should().Contain("id=\"logBox\"");
        detail.Should().Contain("id=\"liveUtilizationCard\"");
        detail.Should().Contain("id=\"btnCancelRun\"");

        var script = Read("DotNet/Link.UI/Views/Automation/_RunDetailScript.cshtml");
        script.Should().Contain("Logs\", \"Automation\"");
        script.Should().Contain("payload.run");
        script.Should().Contain("refreshStoredLogs");
        script.Should().Contain("CancelJson");
        script.Should().Contain("DeleteJson");
        script.Should().Contain("Export");

        var manifest = Read("DotNet/Link.UI/Views/Automation/Manifest.cshtml");
        manifest.Should().Contain("Generated vs ABS");
        manifest.Should().Contain("\"Automation\"");
        manifest.Should().Contain("asp-action=\"Run\"");

        var runJs = Read("DotNet/Link.UI/wwwroot/js/automation-run.js");
        runJs.Should().Contain("getElementById(\"runStatus\") || document.getElementById(\"status\")");
        runJs.Should().Contain("refreshStoredLogs");
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
