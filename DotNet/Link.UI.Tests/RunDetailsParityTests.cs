using Xunit;

namespace Link.UI.Tests;

/// <summary>
/// Phase 2 guard: the Automation run page now lives on the report page. Everything the old run page rendered
/// must still be rendered there, and nothing a run needs may be hidden except the parts the shared cards replace.
/// </summary>
public class RunDetailsParityTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Link.UI")))
            dir = dir.Parent;
        return dir!.FullName;
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root(), .. parts]));

    /// <summary>Every element id the old run page had (the run script binds to all of them).</summary>
    public static readonly string[] OldRunPageIds =
    [
        "advancedPerformanceHost",
        "btnCancelRun",
        "btnDeleteRun",
        "btnLiveGenerate",
        "btnLiveReference",
        "btnLiveUpload",
        "btnViewManifest",
        "closeFlyout",
        "cohortSummaryBody",
        "cohortSummaryCard",
        "dataAcqActiveDuration",
        "dataAcqDonut",
        "dataAcqErrorsBtn",
        "dataAcqLegend",
        "dataAcqRate",
        "dataAcqResourceCount",
        "dataAcqResourceMixChart",
        "dataAcqResPerSec",
        "dataAcqTimeline",
        "dataAcqTotalLogs",
        "entriesDonut",
        "entriesLegend",
        "error",
        "errorFlyout",
        "errorFlyoutBody",
        "errorFlyoutTitle",
        "errorRow",
        "exModalAccordion",
        "exModalEmpty",
        "exModalGroupChips",
        "exModalSearch",
        "exModalVisibleCount",
        "fhirGenerator",
        "finalizingBadge",
        "finalizingBadgeText",
        "finished",
        "generationTemplateVersion",
        "liveAcceptingLabel",
        "liveAdmittedCount",
        "liveDischargedCount",
        "liveEventLog",
        "liveExpectedCount",
        "liveExpectedFilter",
        "liveExpectedHeaderCount",
        "liveExpectedList",
        "liveExpectedShownCount",
        "liveInjectControls",
        "liveMetrics",
        "liveNotAdmittedCount",
        "livePoolBody",
        "livePoolCount",
        "livePoolFilter",
        "livePoolShownCount",
        "livePoolTable",
        "liveSimulationCard",
        "liveUploadFile",
        "liveWindowCountdown",
        "logBox",
        "logNewer",
        "logOlder",
        "logPageInfo",
        "logs-pane",
        "logs-tab",
        "measureActiveDuration",
        "measureErrorsBtn",
        "measureRate",
        "measureResourceCount",
        "measureResourceMixChart",
        "measureResPerSec",
        "measureStageChart",
        "measureStageLegend",
        "measureTimeline",
        "nhsnOrganizationIdSummary",
        "normalizationActiveDuration",
        "normalizationErrorsBtn",
        "normalizationRate",
        "normalizationResourceCount",
        "normalizationResourceMixChart",
        "normalizationResPerSec",
        "normalizationTimeline",
        "overview-pane",
        "overview-tab",
        "pipelineDuration",
        "populationSummary",
        "reportMilestones",
        "runConfigProps",
        "runDetailsTabContent",
        "runDetailsTabs",
        "runHeaderActions",
        "runName",
        "runRetention",
        "runSummaryGrid",
        "scheduleAdhoc",
        "scheduleCreated",
        "scheduleDuration",
        "scheduleEnd",
        "scheduleFrequency",
        "scheduleStart",
        "scheduleStatusPill",
        "scheduleSubmitted",
        "serviceErrorBtnDataAcq",
        "serviceErrorBtnMeasureEval",
        "serviceErrorBtnNormalization",
        "serviceErrorBtnReport",
        "serviceErrorBtnValidation",
        "serviceErrorCountDataAcq",
        "serviceErrorCountMeasureEval",
        "serviceErrorCountNormalization",
        "serviceErrorCountReport",
        "serviceErrorCountValidation",
        "serviceErrorSummaryCard",
        "serviceExceptionModal",
        "serviceExceptionModalLabel",
        "serviceExceptionSubtitle",
        "started",
        "status",
        "testConfigCard",
        "validationActiveDuration",
        "validationErrorsBtn",
        "validationFailedCount",
        "validationHoldBanner",
        "validationHoldText",
        "validationOngoingBadge",
        "validationPassedCount",
        "validationPatientCount",
        "validationStageChart",
        "validationStageLegend",
        "validationTimeline",
        "validatorResultsCard",
        "validatorResultsGrid",
        "validatorResultsSummary",
    ];

    [Fact]
    public void Every_old_run_page_element_is_still_rendered()
    {
        var detail = Read("Link.UI", "Views", "Automation", "_RunDetail.cshtml");
        var missing = OldRunPageIds.Where(id => !detail.Contains($"id=\"{id}\"", StringComparison.Ordinal)).ToList();
        Assert.True(missing.Count == 0, "Lost from the run section: " + string.Join(", ", missing));
    }

    [Fact]
    public void Report_page_embeds_the_whole_run_section_and_its_scripts()
    {
        var report = Read("Link.UI", "Views", "Tenants", "Report.cshtml");
        var automation = Read("Link.UI", "Views", "Tenants", "_ReportAutomation.cshtml");
        Assert.Contains("~/Views/Tenants/_ReportAutomation.cshtml", report);
        // The run-only view renders this file from AutomationController, so Tenants partials need full paths.
        foreach (var local in new[] { "_ReportAutomation", "_ReportLive" })
            Assert.DoesNotContain($"<partial name=\"{local}\"", report);
        Assert.Contains("_RunDetailScript.cshtml", report);
        Assert.Contains("automation-run.js", report);
        Assert.Contains("live-utilization.js", report);
        Assert.Contains("chart.umd.min.js", report);
        Assert.Contains("_RunDetail.cshtml", automation);
        Assert.Contains("[\"RunEmbedded\"] = true", automation);
        Assert.Contains("id=\"liveStatus\"", automation);
        Assert.Contains("_DataAcquisitionLogsModal", automation);
        // Runs that never created a report: same page, clean empty state, still watching for the report.
        Assert.Contains("RunOnly", report);
        Assert.Contains("_RunReportWatch", report);
        var emptyAt = report.IndexOf("This run ended without creating a report.", StringComparison.Ordinal);
        Assert.True(emptyAt > 0);
        var branch = report.LastIndexOf("@if (Model.RunOnly)", emptyAt, StringComparison.Ordinal);
        Assert.True(branch >= 0);
        Assert.DoesNotContain("run?.Run is not null", report.Substring(branch, emptyAt - branch), StringComparison.Ordinal);
    }

    [Fact]
    public void Every_full_path_partial_on_the_report_page_exists()
    {
        foreach (var file in new[] { "Report.cshtml", "_ReportAutomation.cshtml" })
        {
            var view = Read("Link.UI", "Views", "Tenants", file);
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(view, @"""~/(Views/[^""]+\.cshtml)"""))
                Assert.True(File.Exists(Path.Combine([Root(), "Link.UI", .. m.Groups[1].Value.Split('/')])), $"{file}: {m.Groups[1].Value} does not exist");
        }
    }

    [Fact]
    public void Only_the_duplicated_report_detail_is_hidden_when_embedded()
    {
        var detail = Read("Link.UI", "Views", "Automation", "_RunDetail.cshtml");
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(detail, @"embedded \? ""d-none"""));
        Assert.Contains("id=\"runReportDetail\"", detail);
        Assert.Contains("@(embedded ? \"Milestones\" : \"Report\")", detail);
    }

    [Fact]
    public void Run_section_links_name_the_Automation_controller()
    {
        var detail = Read("Link.UI", "Views", "Automation", "_RunDetail.cshtml");
        Assert.DoesNotMatch(@"<a asp-action=", detail);
        Assert.DoesNotMatch(@"Url\.Action\(""[^""]+""\)", detail);
    }

    [Fact]
    public void Automation_cards_only_on_test_facilities_with_Automation_on()
    {
        var tenants = Read("Link.UI", "Controllers", "TenantsController.cs");
        Assert.Contains("if (!page.IsTest && !reportGone)", tenants);
        Assert.Contains("features?.AutomationEnabled != true", tenants);
    }

    [Fact]
    public void Old_run_url_redirects_unless_the_legacy_flag_is_on()
    {
        var controller = Read("Link.UI", "Controllers", "AutomationController.cs");
        Assert.Contains("LegacyRunPage", controller);
        Assert.Contains("Redirect(Url.Action(\"Report\", \"Tenants\"", controller);
        Assert.Contains("View(\"~/Views/Tenants/Report.cshtml\"", controller);
        Assert.Contains("LinkUi:LegacyRunPage", Read("Link.UI", "Program.cs"));
    }

    [Fact]
    public void Progress_tracker_polls_summaries_not_patients()
    {
        var tracker = Read("Automation.Link", "Helpers", "PipelineProgressTracker.cs");
        Assert.DoesNotContain("GetReportEntriesWithMeasureReportsAsync", tracker);
        Assert.Contains("GetReportEntrySummaryAsync", tracker);
    }

    [Fact]
    public void Poller_reads_per_patient_domains_on_the_slow_cadence_only()
    {
        Assert.True(Automation.UI.Services.StoreBackedServicePoller.FullPollInterval >= TimeSpan.FromSeconds(30));
        var last = new LantanaGroup.Link.Automation.Link.Helpers.PipelineDataReader.ReportEntryRollup
        {
            EntryCount = 1,
            NotReportableMeasureCount = 4,
            EntriesWithMeasureReport = 1
        };
        var merged = Automation.UI.Services.StoreBackedServicePoller.MergeCounts(
            last,
            new Dictionary<string, int> { ["PassedValidation"] = 3, ["PatientIdentified"] = 2 },
            new Dictionary<string, int> { ["Submitted"] = 3, ["Pending"] = 2 });
        Assert.Equal(5, merged.EntryCount);
        Assert.Equal(3, merged.ValidatedCount);
        Assert.False(merged.AllReportingTerminal);
        Assert.Equal(4, merged.NotReportableMeasureCount); // measure-level fields kept until the next full read
        Assert.Contains(merged.SubmissionStatuses, s => s.Status == "Submitted" && s.Count == 3);
    }
}