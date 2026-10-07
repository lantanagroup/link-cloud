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
        list.Should().Contain("Process the selected acquisition logs?");
        list.Should().Contain("Cancel the selected logs that are old enough? This cannot be undone.");
        list.Should().Contain("Process every acquisition log that matches these filters?");
        list.Should().Contain("Cancel every matching log that is old enough? This cannot be undone.");
        list.Should().Contain("Disable every acquisition log for this facility?");
        list.Should().Contain("Restore every acquisition log for this facility?");
        list.Should().Contain("data-da-panel");

        var page = Read("DotNet/Link.UI/Views/Logs/Acquisition.cshtml");
        page.Should().Contain("name=\"_AcquisitionLogList\"");
        page.Should().NotContain("Process the selected acquisition logs?");

        var detail = Read("DotNet/Link.UI/Views/Automation/_RunDetail.cshtml");
        detail.Should().Contain("data-bs-target=\"#dataAcqLogModal\"");
        detail.Should().NotContain("id=\"dataAcqLogTable\"");

        var script = Read("DotNet/Link.UI/Views/Automation/_RunDetailScript.cshtml");
        script.Should().NotContain("loadDataAcqLogs");

        var modal = Read("DotNet/Link.UI/Views/Shared/_DataAcquisitionLogsModal.cshtml");
        modal.Should().Contain("id=\"dataAcqLogModal\"");
        modal.Should().Contain("data-acquisition-logs.js");

        Read("DotNet/Link.UI/Views/Automation/Run.cshtml").Should().Contain("name=\"_DataAcquisitionLogsModal\"");
        Read("DotNet/Link.UI/Views/Tenants/View.cshtml").Should().Contain("name=\"_DataAcquisitionLogsModal\"");
        Read("DotNet/Link.UI/Views/Tenants/Facility.cshtml").Should().Contain("name=\"_DataAcquisitionLogsModal\"");

        var module = Read("DotNet/Link.UI/wwwroot/js/data-acquisition-logs.js");
        module.Should().Contain("12000");
        module.Should().Contain("searchTerm");

        var controller = Read("DotNet/Link.UI/Controllers/LogsController.cs");
        controller.Should().Contain("AcquisitionLogPanelRules.AllowsRunEntry");
        controller.Should().NotContain("RunOwns");
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
