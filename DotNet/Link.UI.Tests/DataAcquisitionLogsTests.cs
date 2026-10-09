using FluentAssertions;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class DataAcquisitionLogsTests
{
    [Fact]
    public void The_run_entry_exists_only_while_automation_is_on()
    {
        AcquisitionLogPanelRules.AllowsRunEntry(true).Should().BeTrue();
        AcquisitionLogPanelRules.AllowsRunEntry(false).Should().BeFalse();
    }

    [Fact]
    public void A_named_facility_is_listed_directly()
    {
        AcquisitionLogPanelRules.ScopeForEntry(namedFacility: true).Should().Be(AutomationMarkRules.All);
        AcquisitionLogPanelRules.ScopeForEntry(namedFacility: false).Should().BeNull();
    }

    [Fact]
    public void The_shared_list_is_the_only_acquisition_log_presentation()
    {
        var list = Read("DotNet/Link.UI/Views/Logs/_AcquisitionLogList.cshtml");
        list.Should().Contain("data-au-bulk=\"process\"");
        list.Should().Contain("data-au-select-page");
        var live = Read("DotNet/Link.UI/wwwroot/js/live-region.js");
        live.Should().Contain("Process \" + count + \" selected acquisition logs?");
        live.Should().Contain("Cancel \" + count + \" selected logs that are old enough? This cannot be undone.");
        list.Should().Contain("Process every acquisition log that matches these filters?");
        list.Should().Contain("Cancel every matching log that is old enough? This cannot be undone.");
        list.Should().Contain("Disable every acquisition log for this facility?");
        list.Should().Contain("Restore every acquisition log for this facility?");
        list.Should().Contain("data-da-panel");

        var page = Read("DotNet/Link.UI/Views/Logs/Acquisition.cshtml");
        page.Should().Contain("name=\"_AcquisitionLogList\"");
        page.Should().NotContain("data-au-bulk=\"process\"");

        var detail = Read("DotNet/Link.UI/Views/Automation/_RunDetail.cshtml");
        detail.Should().Contain("data-bs-target=\"#dataAcqLogModal\"");
        detail.Should().NotContain("id=\"dataAcqLogTable\"");

        var script = Read("DotNet/Link.UI/Views/Automation/_RunDetailScript.cshtml");
        script.Should().NotContain("loadDataAcqLogs");

        var modal = Read("DotNet/Link.UI/Views/Shared/_DataAcquisitionLogsModal.cshtml");
        modal.Should().Contain("id=\"dataAcqLogModal\"");
        modal.Should().Contain("data-da-host");
        modal.Should().Contain("data-da-list");
        modal.Should().Contain("data-acquisition-logs.js");

        var report = Read("DotNet/Link.UI/Views/Reports/Acquisition.cshtml");
        report.Should().Contain("data-da-host");
        report.Should().Contain("data-da-autoload");
        report.Should().NotContain("Open in Logs");
        report.Should().NotContain("Logs for this report");
        report.Should().Contain("_ReportIdentity");
        report.Should().Contain("_BackButton");

        list.Should().Contain("Facility actions");
        list.Should().Contain("data-da-col=\"facility\"");
        list.Should().Contain("data-da-col=\"report\"");
        list.Should().Contain("Open in Logs");
        Read("DotNet/Link.UI/Views/Logs/Acquisition.cshtml").Should().Contain("btn-check");
        Read("DotNet/Link.UI/Views/Logs/Acquisition.cshtml").Should().NotContain("multiple size");
        Read("DotNet/Link.UI/Controllers/ReportsController.cs").Should().Contain("LoadSectionAsync");
        Read("DotNet/Link.UI/Services/ReportsService.cs").Should().NotContain("SearchAcquisitionLogsAsync");

        Read("DotNet/Link.UI/Views/Automation/Run.cshtml").Should().Contain("name=\"_DataAcquisitionLogsModal\"");
        Read("DotNet/Link.UI/Views/Tenants/View.cshtml").Should().Contain("name=\"_DataAcquisitionLogsModal\"");
        Read("DotNet/Link.UI/Views/Tenants/Facility.cshtml").Should().Contain("name=\"_DataAcquisitionLogsModal\"");

        var module = Read("DotNet/Link.UI/wwwroot/js/data-acquisition-logs.js");
        module.Should().Contain("12000");
        module.Should().Contain("searchTerm");
        module.Should().Contain("data-da-host");
        module.Should().Contain("restoreChecked");
        module.Should().Contain("removeAttribute(\"data-au-save\")");
        module.Should().Contain("load(host, scopeUrl(host))");
        module.Should().NotContain("applyPage");

        var controller = Read("DotNet/Link.UI/Controllers/LogsController.cs");
        controller.Should().Contain("AcquisitionLogPanelRules.AllowsRunEntry");
        controller.Should().NotContain("RunOwns");
    }

    [Fact]
    public void The_shared_list_renders_times_through_the_shared_painter()
    {
        Read("DotNet/Link.UI/Services/LogsService.cs").Should().Contain("FacilityViewRules.When(row.CreateDate)");
        Read("DotNet/Link.UI/Services/FacilityViewRules.cs").Should().Contain("LinkUiTime.Display");
        Read("DotNet/Link.UI/Views/Logs/_AcquisitionLogList.cshtml").Should().Contain("@row.Created");

        var painter = Read("DotNet/Link.UI/wwwroot/js/live-region.js");
        painter.Should().Contain(" UTC");
        painter.Should().Contain("window.luPaintTimes = paintTimes");

        var module = Read("DotNet/Link.UI/wwwroot/js/data-acquisition-logs.js");
        module.Should().Contain("window.luPaintTimes");
        Read("DotNet/Link.UI/wwwroot/js/automation-run.js").Should().Contain("window.luPaintTimes");
    }

    [Fact]
    public void Log_actions_use_green_and_solid_red()
    {
        var pages = new[]
        {
            "DotNet/Link.UI/Views/Logs/_AcquisitionLogList.cshtml",
            "DotNet/Link.UI/Views/Logs/AcquisitionDetail.cshtml",
            "DotNet/Link.UI/Views/Logs/Acquisition.cshtml",
            "DotNet/Link.UI/Views/Logs/Audit.cshtml",
            "DotNet/Link.UI/Views/Logs/Sftp.cshtml",
            "DotNet/Link.UI/Views/Logs/SftpDetail.cshtml",
            "DotNet/Link.UI/Views/Logs/Index.cshtml",
            "DotNet/Link.UI/Views/Logs/Kafka.cshtml"
        };
        foreach (var page in pages)
        {
            var text = Read(page);
            text.Should().NotContain("btn-au-action", because: page);
            text.Should().NotContain("btn-primary", because: page);
            text.Should().NotContain("btn-outline-danger", because: page);
        }

        var list = Read("DotNet/Link.UI/Views/Logs/_AcquisitionLogList.cshtml");
        list.Should().Contain("btn btn-sm btn-au-execute\">Process matching");
        list.Should().Contain("btn btn-sm btn-au-execute\" data-au-bulk=\"process\"");
        list.Should().Contain("data-au-bulk=\"process\"");
        list.Should().Contain("data-au-select-page");
        list.Should().Contain("cannot be selected.");
        list.Should().Contain("btn btn-sm btn-danger\">Disable logs");
        list.Should().Contain("btn btn-sm btn-warning\">Cancel matching");
        list.Should().Contain("btn btn-sm btn-au-execute lu-icon-btn\" formaction");
        list.Should().Contain("btn btn-sm btn-warning\" formaction");
        list.Should().Contain("btn-success\">Restore logs");
        list.Should().Contain("btn-au-link\" href");

        var detail = Read("DotNet/Link.UI/Views/Logs/AcquisitionDetail.cshtml");
        detail.Should().Contain("btn btn-sm btn-au-execute\">Process");
        detail.Should().Contain("btn btn-sm btn-warning\">Cancel");

        foreach (var searchPage in new[] { "Acquisition", "Audit", "Sftp" })
        {
            Read($"DotNet/Link.UI/Views/Logs/{searchPage}.cshtml")
                .Should().Contain("btn btn-success\"><i class=\"bi bi-search me-1\"></i>Search");
        }

        var live = Read("DotNet/Link.UI/wwwroot/js/live-region.js");
        live.Should().Contain("restoreChecked");
        live.Should().Contain("Process \" + process + \" selected");
        live.Should().Contain("Cancel \" + cancel + \" selected");

        Read("DotNet/Link.UI/wwwroot/css/site.css").Should().Contain(".btn-danger:hover");
        Read("DotNet/Link.UI/wwwroot/css/site.css").Should().Contain("--lu-dialog-top");
    }

    [Fact]
    public void Home_and_the_editor_do_not_substitute_another_name_for_a_facility()
    {
        Read("DotNet/Link.UI/Views/Home/_Overview.cshtml").Should().NotContain("regular");
        Read("DotNet/Link.UI/Views/Tenants/Facility.cshtml").Should().NotContain("hospital");
        Read("DotNet/Link.UI/Views/Tenants/Facility.cshtml").Should().Contain("throwaway facility");
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
