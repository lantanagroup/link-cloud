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
        page.Should().Contain("btn-au-neutral");
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
        css.Should().Contain(".btn-au-neutral");

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
            text.Should().Contain("id=\"resubmitDialog\"");
            text.Should().Contain("id=\"resubmitForm\"");
            text.Should().Contain("data-resubmit-open");
            text.Should().Contain("name=\"bypassSubmission\" value=\"true\">Regenerate without submitting");
            text.Should().NotContain("Bypass submission");
            text.Should().NotContain("type=\"checkbox\" name=\"bypassSubmission\"");
        }

        var js = File.ReadAllText(RepoFile("DotNet/Link.UI/wwwroot/js/live-region.js"));
        js.Should().Contain("new FormData(form, submitter)");
        js.Should().Contain("data-resubmit-open");
        js.Should().Contain("function liftDialogs()");
        js.Should().Contain("\".modal, .offcanvas\"");
        js.Should().Contain("data-lu-lifted");
        js.Should().Contain("function dropLiftedDialogs()");
        var swap = js.IndexOf("function swapContent(", StringComparison.Ordinal);
        swap.Should().BeGreaterThan(-1);
        js.IndexOf("dropLiftedDialogs()", swap, StringComparison.Ordinal).Should().BeGreaterThan(swap);
    }

    [Fact]
    public void Generate_report_posts_bypass_as_a_labeled_choice()
    {
        var text = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Reports/Generate.cshtml"));
        text.Should().Contain("name=\"bypassSubmission\" value=\"false\"");
        text.Should().Contain("name=\"bypassSubmission\" value=\"true\"");
        text.Should().Contain("Generate and submit");
        text.Should().Contain("Generate without submitting");
        text.Should().NotContain("type=\"checkbox\" name=\"bypassSubmission\"");
    }

    [Fact]
    public void Roles_add_starts_collapsed()
    {
        var text = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/System/Roles.cshtml"));
        text.Should().Contain("data-lu-reveal=\"roleCreate\"");
        text.Should().Contain("id=\"roleCreate\"");
        text.Should().Contain("data-lu-cancel");
        text.Should().Contain("btn btn-success\">Save");
        text.Should().Contain("id=\"roleResults\" data-au-refresh=\"20000\"");
        text.Should().NotContain("Create role");
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
        plan.Should().Contain("id=\"queryPlanCount\"");
        plan.Should().Contain("data-plan-type=");
        plan.Should().NotContain("id=\"queryPlanType\"");
        plan.Should().NotContain("asp-route-planType=\"@planType\"");

        var facility = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Tenants/Facility.cshtml"));
        facility.Should().Contain("class=\"text-muted small mt-3 mb-0 lu-facility-note\"");
        facility.Should().Contain("each row has its own Delete");
        facility.Should().Contain("A single setting shows + Add");
        facility.Should().Contain("id=\"dispatchCount\"");
        facility.Should().Contain("data-row-remove");
        facility.Should().Contain("id=\"notificationCount\"");
        facility.Should().Contain("id=\"notificationEmails\"");
        facility.Should().NotContain("type=\"checkbox\" name=\"Schedules");

        js.Should().Contain("reconcileEmptied");
        js.Should().Contain("data-collection-clear");
        js.Should().Contain("data-facility-edited");
        js.Should().Contain("!pending && !wasDirty");
        var normalization = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Shared/_NormalizationOperationEditor.cshtml"));
        normalization.Should().Contain("id=\"normalizationCount\"");
        normalization.Should().Contain("data-facility-swap=\"normalizationPanel\"");

        var css = File.ReadAllText(RepoFile("DotNet/Link.UI/wwwroot/css/site.css"));
        css.Should().Contain("#facilityPanels > .accordion-item > .accordion-header");
        css.Should().Contain(".btn-success:disabled");
        css.Should().Contain("background-color: var(--au-success)");
        css.Should().Contain("#auToasts");
        css.Should().Contain(".btn-warning:hover");
    }

    [Fact]
    public void Section_warning_keeps_the_add_editor_visible()
    {
        var js = File.ReadAllText(RepoFile("DotNet/Link.UI/wwwroot/js/facility-save.js"));
        var start = js.IndexOf("function collapseEmpty(", StringComparison.Ordinal);
        var end = js.IndexOf("function saveNameFor(", start, StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1);
        end.Should().BeGreaterThan(start);
        var body = js.Substring(start, end - start);
        var warn = body.IndexOf("alert-warning", StringComparison.Ordinal);
        var hide = body.IndexOf("form.classList.add(\"d-none\")", StringComparison.Ordinal);
        warn.Should().BeGreaterThan(-1);
        hide.Should().BeGreaterThan(warn);
    }

    [Fact]
    public void Saved_sftp_test_reads_the_result_body()
    {
        var service = File.ReadAllText(RepoFile("DotNet/Link.UI/Services/FacilityAcquisitionService.cs"));
        var start = service.IndexOf("Task<string?> TestSavedSftpAsync(", StringComparison.Ordinal);
        var end = service.IndexOf("Task<string?> TestSftpAsync(", start, StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1);
        end.Should().BeGreaterThan(start);
        var body = service.Substring(start, end - start);
        body.Should().Contain("response.Body is not { Success: true }");
        body.Should().Contain("response.Body?.Message");

        var client = File.ReadAllText(RepoFile("DotNet/LinkSdk/Clients/DataAcquisitionServiceClient.cs"));
        client.Should().Contain("SendAsync<SftpTestConnectionResultApiModel>(() => Request($\"data/{organizationId}/sftp-configurations/test-connection\")");
    }

    [Fact]
    public void Facility_section_swap_runs_inline_scripts_and_stays_open()
    {
        var js = File.ReadAllText(RepoFile("DotNet/Link.UI/wwwroot/js/facility-save.js"));
        var start = js.IndexOf("function runInlineScripts(", StringComparison.Ordinal);
        var end = js.IndexOf("function updateCounts(", start, StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1);
        end.Should().BeGreaterThan(start);
        var body = js.Substring(start, end - start);
        body.Should().Contain("function swapItem(");
        body.Should().Contain("runInlineScripts(fresh)");
        body.Should().Contain("createElement(\"script\")");
        body.Should().Contain("old.textContent");
        body.Should().Contain("accordion-collapse.show");
        body.Should().Contain("aria-expanded\", \"true\"");
    }

    [Fact]
    public void Test_operation_redirects_back_to_the_facility_page()
    {
        var text = File.ReadAllText(RepoFile("DotNet/Link.UI/Controllers/TenantsController.cs"));
        var start = text.IndexOf("Task<IActionResult> TestOperation(", StringComparison.Ordinal);
        var end = text.IndexOf("Task<IActionResult> View(", start, StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1);
        end.Should().BeGreaterThan(start);
        var body = text.Substring(start, end - start);
        body.Should().Contain("RedirectNormalizationStay(result, operation)");
        body.Should().NotContain("return await FromResult(result, id ?? \"Facility\");");

        var redirect = text.IndexOf("RedirectNormalizationStay(", StringComparison.Ordinal);
        redirect.Should().BeGreaterThan(-1);
        text.Should().Contain("TempData[\"NormalizationTestResult\"]");
        text.Should().Contain("RestoreNormalizationTempData(page)");
        var import = text.IndexOf("Task<IActionResult> ImportExtensionUrls(", StringComparison.Ordinal);
        var importEnd = text.IndexOf("Task<IActionResult> DeleteOperation(", import, StringComparison.Ordinal);
        import.Should().BeGreaterThan(-1);
        importEnd.Should().BeGreaterThan(import);
        text.Substring(import, importEnd - import).Should().Contain("RedirectNormalizationStay(result, operationId: null)");
    }

    [Fact]
    public void Report_counts_survive_a_page_refresh()
    {
        var text = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Reports/Index.cshtml"));
        text.Should().Contain("auReportCountsBound");
        text.Should().Contain("lu-content");
    }

    [Fact]
    public void Patient_configuration_section_badges_keep_a_gap_after_the_header()
    {
        var js = File.ReadAllText(RepoFile("DotNet/Link.UI/wwwroot/js/patient-configuration-editor.js"));
        var assignments = js.Split('\n')
            .Where(line => line.Contains("pc-section-badge", StringComparison.Ordinal))
            .ToList();
        assignments.Should().NotBeEmpty();
        assignments.Should().OnlyContain(line => line.Contains("ms-2", StringComparison.Ordinal));

        var markup = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Shared/_PatientConfigurationEditor.cshtml"));
        Count(markup, "pc-section-badge bg-light text-muted border ms-2").Should().Be(4);
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
