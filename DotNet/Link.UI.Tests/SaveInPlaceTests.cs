using FluentAssertions;
using Xunit;

namespace Link.UI.Tests;

public class SaveInPlaceTests
{
    [Theory]
    [InlineData("DotNet/Link.UI/Views/Measures/Index.cshtml")]
    [InlineData("DotNet/Link.UI/Views/QueryPlans/Index.cshtml")]
    [InlineData("DotNet/Link.UI/Views/Normalizations/Index.cshtml")]
    [InlineData("DotNet/Link.UI/Views/OrganizationResourceMaps/Index.cshtml")]
    [InlineData("DotNet/Link.UI/Views/FacilityTemplates/Index.cshtml")]
    [InlineData("DotNet/Link.UI/Views/PatientConfigurations/Index.cshtml")]
    [InlineData("DotNet/Link.UI/Views/Automation/Scenarios.cshtml")]
    public void Configuration_editors_refresh_the_open_page(string relativePath)
    {
        var text = File.ReadAllText(RepoFile(relativePath));
        text.Should().NotContain("location.reload(");
        text.Should().Contain("auRefreshPage(");
    }

    [Fact]
    public void Patient_configuration_empty_name_reaches_the_server()
    {
        var text = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/PatientConfigurations/Index.cshtml"));
        text.Should().NotContain("if (!model.name || !model.name.trim())");
        text.Should().Contain("pcFormErrors");
    }

    [Theory]
    [InlineData("DotNet/Link.UI/Views/Tenants/Facility.cshtml", 6)]
    [InlineData("DotNet/Link.UI/Views/System/Integration.cshtml", 8)]
    [InlineData("DotNet/Link.UI/Views/Shared/_CensusEditor.cshtml", 2)]
    [InlineData("DotNet/Link.UI/Views/Shared/_FhirQueryEditor.cshtml", 13)]
    [InlineData("DotNet/Link.UI/Views/Shared/_NormalizationOperationEditor.cshtml", 7)]
    public void Editor_posts_save_in_place(string relativePath, int forms)
    {
        var text = File.ReadAllText(RepoFile(relativePath));
        Count(text, "<form method=\"post\" data-au-save").Should().Be(forms);
        text.Should().NotContain("<form method=\"post\" asp-");
        text.Should().NotContain("<form method=\"post\"\r");
        text.Should().NotContain("<form method=\"post\"\n");
    }

    [Fact]
    public void Refresh_keeps_the_page_and_lifts_the_success_alert()
    {
        var js = File.ReadAllText(RepoFile("DotNet/Link.UI/wwwroot/js/live-region.js"));
        js.Should().Contain("window.auRefreshPage = function");
        js.Should().Contain("alert-success");
        js.Should().Contain("data-au-rerun");
        js.Should().Contain("Could not refresh this page.");

        var css = File.ReadAllText(RepoFile("DotNet/Link.UI/wwwroot/css/site.css"));
        css.Should().Contain(".lu-main:has(.lu-content h2.fw-bold) .lu-topbar");
        css.Should().Contain("#reportResults th:nth-child(4)");
        css.Should().Contain("#facilityReportResults th:nth-child(3)");
    }

    [Fact]
    public void Save_posts_the_form_action_unless_the_button_sets_one()
    {
        var js = File.ReadAllText(RepoFile("DotNet/Link.UI/wwwroot/js/live-region.js"));
        js.Should().Contain("submitter.hasAttribute(\"formaction\")");
        js.Should().NotContain("submitter.formAction) || form.action");
    }

    [Fact]
    public void Facility_editor_saves_once_and_drops_the_sdk_label()
    {
        var page = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Tenants/Facility.cshtml"));
        page.Should().Contain("id=\"facilitySaveBar\"");
        page.Should().Contain("btn-light");
        page.Should().NotContain("LinkSDK");

        var js = File.ReadAllText(RepoFile("DotNet/Link.UI/wwwroot/js/facility-save.js"));
        var saveFacility = js.IndexOf("\"SaveFacility\"", StringComparison.Ordinal);
        var saveCensus = js.IndexOf("\"SaveCensus\"", StringComparison.Ordinal);
        var deleteCensus = js.IndexOf("\"DeleteCensus\"", StringComparison.Ordinal);
        var saveNotification = js.IndexOf("\"SaveNotification\"", StringComparison.Ordinal);
        var deleteNotification = js.IndexOf("\"DeleteNotification\"", StringComparison.Ordinal);
        saveFacility.Should().BeGreaterThan(-1);
        saveCensus.Should().BeGreaterThan(saveFacility);
        saveNotification.Should().BeGreaterThan(saveCensus);
        deleteCensus.Should().BeGreaterThan(saveNotification);
        deleteNotification.Should().BeGreaterThan(deleteCensus);
        js.Should().Contain("beforeunload");
        js.Should().Contain("show.bs.tab");
        js.Should().Contain("form.action");

        var css = File.ReadAllText(RepoFile("DotNet/Link.UI/wwwroot/css/site.css"));
        css.Should().Contain(".badge.text-bg-light");
        css.Should().Contain(".lu-admin .btn-outline-danger");

        var layout = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Shared/_Layout.cshtml"));
        layout.Should().Contain("lu-admin");
    }

    [Fact]
    public void Resubmit_is_a_solid_warning_button()
    {
        foreach (var relative in new[]
        {
            "DotNet/Link.UI/Views/Reports/Index.cshtml",
            "DotNet/Link.UI/Views/Tenants/_ViewReports.cshtml"
        })
        {
            var text = File.ReadAllText(RepoFile(relative));
            text.Should().Contain("btn btn-sm btn-warning\">Resubmit");
            text.Should().NotContain("btn-outline-primary\">Resubmit");
        }
    }

    [Fact]
    public void Facility_section_actions_live_on_the_header()
    {
        var js = File.ReadAllText(RepoFile("DotNet/Link.UI/wwwroot/js/facility-save.js"));
        js.Should().Contain("lu-section-action");
        js.Should().Contain("\"+ Add\"");
        js.Should().Contain("Staged for removal");
        js.Should().NotContain("lu-section-empty");
        js.Should().Contain("data-facility-swap");
        var swapAt = js.IndexOf("closest(\"a[data-facility-swap]\")", StringComparison.Ordinal);
        swapAt.Should().BeGreaterThan(-1);
        var stopAt = js.IndexOf("stopImmediatePropagation()", swapAt, StringComparison.Ordinal);
        stopAt.Should().BeGreaterThan(swapAt);
        js.IndexOf("Leave without saving", stopAt, StringComparison.Ordinal).Should().BeGreaterThan(stopAt);
        var deleteAt = js.IndexOf("button.textContent = \"Delete\"", StringComparison.Ordinal);
        deleteAt.Should().BeGreaterThan(-1);

        var plan = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Shared/_FhirQueryEditor.cshtml"));
        plan.Should().Contain("id=\"queryPlanType\"");
        plan.Should().NotContain("asp-route-planType=\"@planType\"");

        var normalization = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Shared/_NormalizationOperationEditor.cshtml"));
        normalization.Should().Contain("data-facility-swap=\"normalizationPanel\"");

        var css = File.ReadAllText(RepoFile("DotNet/Link.UI/wwwroot/css/site.css"));
        css.Should().Contain("#facilityPanels > .accordion-item > .accordion-header");
        css.Should().Contain(".btn-warning:hover");
    }

    [Fact]
    public void Report_counts_survive_a_page_refresh()
    {
        var text = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Reports/Index.cshtml"));
        text.Should().Contain("auReportCountsBound");
        text.Should().Contain("lu-content");
    }

    private static int Count(string text, string value)
    {
        var count = 0;
        var start = 0;
        while (true)
        {
            var at = text.IndexOf(value, start, StringComparison.Ordinal);
            if (at < 0) return count;
            count++;
            start = at + value.Length;
        }
    }

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;

        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
    }
}
