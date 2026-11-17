using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using Link.UI.Models;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

/// <summary>Report page progress: stage, pipeline counts, submission breakdown, patients per measure, time to submit.</summary>
public class ReportProgressTests
{
    private static readonly DateTime Now = new(2026, 10, 11, 23, 30, 0, DateTimeKind.Utc);
    private const string Ip = "NHSNAcuteCareHospitalMonthlyInitialPopulation";

    private static ReportScheduleApiModel Schedule(ScheduleStatus status, DateTime? created = null, DateTime? submitted = null) => new()
    {
        Id = Guid.NewGuid(),
        FacilityId = "mercy-general",
        ReportTypes = [Ip],
        ReportStartDate = new DateTime(2022, 12, 1, 0, 0, 0, DateTimeKind.Utc),
        ReportEndDate = new DateTime(2022, 12, 31, 23, 59, 59, DateTimeKind.Utc),
        CreateDate = created ?? new DateTime(2026, 10, 11, 23, 0, 0, DateTimeKind.Utc),
        SubmitReportDateTime = submitted,
        Status = status
    };

    private static ReportEntrySummaryApiModel Entries(int identified, int pending, int passed, int submitted, int submitting = 0) => new()
    {
        ReportTypeCounts = new() { [Ip] = identified + pending + passed },
        ReportingStatusCounts = new()
        {
            ["PatientIdentified"] = identified,
            ["PendingValidation"] = pending,
            ["PassedValidation"] = passed
        },
        SubmissionStatusCounts = new() { ["Submitted"] = submitted, ["Submitting"] = submitting }
    };

    private static DataAcquisitionReportSummaryApiModel Acquisition(int completed, int processing) => new()
    {
        TotalLogs = completed + processing,
        TotalPatients = 20,
        TotalResourcesAcquired = 600,
        StatusCounts =
        [
            new() { Status = "Completed", Count = completed },
            new() { Status = "Processing", Count = processing }
        ]
    };

    [Fact]
    public void In_progress_report_shows_the_first_stage_still_working_with_running_counts()
    {
        var progress = ReportProgressService.Build(
            Schedule(ScheduleStatus.EndOfPeriod),
            Entries(identified: 7, pending: 3, passed: 10, submitted: 0),
            null,
            Acquisition(completed: 200, processing: 20),
            new NormalizationReportSummaryApiModel { Passes = 200, ResourcesNormalized = 590, LastNormalizedUtc = Now.AddSeconds(-10) },
            new Dictionary<string, int> { [Ip] = 10 },
            Now);

        Assert.False(progress.Terminal);
        Assert.Equal("Data acquisition", progress.Stage);
        var rows = progress.Pipeline.ToDictionary(r => r.Key);
        Assert.Equal(PipelineState.Active, rows["acquisition"].State);
        Assert.Equal(20, rows["acquisition"].Pending);
        Assert.Equal(PipelineState.Active, rows["normalization"].State);
        Assert.Equal(PipelineState.Active, rows["evaluation"].State);
        Assert.Equal(7, rows["evaluation"].Pending);
        Assert.Equal(3, rows["validation"].Pending);
        Assert.Contains(rows["normalization"].Counts, c => c.Label == "Resources" && c.Value == 590);
        Assert.True(progress.TimeToSubmitRunning);
        Assert.Equal(TimeSpan.FromMinutes(30), progress.TimeToSubmit);
        var measure = Assert.Single(progress.Measures);
        Assert.Equal(20, measure.Patients);
        Assert.Equal(13, measure.Evaluated);
        Assert.Equal(10, measure.InitialPopulation);
    }

    [Fact]
    public void Submitted_report_is_final_and_time_to_submit_runs_from_the_later_of_period_end_and_creation()
    {
        var created = new DateTime(2026, 10, 11, 23, 0, 0, DateTimeKind.Utc);
        var progress = ReportProgressService.Build(
            Schedule(ScheduleStatus.Submitted, created, created.AddMinutes(4)),
            Entries(0, 0, 20, 20), null, Acquisition(220, 0),
            new NormalizationReportSummaryApiModel { Passes = 220, ResourcesNormalized = 653 },
            new Dictionary<string, int> { [Ip] = 20 }, Now);

        Assert.True(progress.Terminal);
        Assert.Equal("Submitted", progress.Stage);
        Assert.All(progress.Pipeline, r => Assert.Equal(PipelineState.Done, r.State));
        Assert.Equal(TimeSpan.FromMinutes(4), progress.TimeToSubmit);
        Assert.False(progress.TimeToSubmitRunning);
        var slice = Assert.Single(progress.Submission);
        Assert.Equal("Submitted", slice.Name);
        Assert.Equal("var(--au-success)", slice.Color);
    }

    [Fact]
    public void Report_created_before_its_period_ended_measures_from_the_period_end()
    {
        var schedule = Schedule(ScheduleStatus.Submitted, new DateTime(2022, 11, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2023, 1, 1, 1, 0, 1, DateTimeKind.Utc));
        var progress = ReportProgressService.Build(schedule, Entries(0, 0, 1, 1), null, null, null, null, Now);
        Assert.Equal(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(2), progress.TimeToSubmit);
    }

    [Fact]
    public void Unreachable_services_render_as_not_available_instead_of_failing()
    {
        var progress = ReportProgressService.Build(Schedule(ScheduleStatus.EndOfPeriod), null, null, null, null, null, Now);
        Assert.All(progress.Pipeline, r => Assert.Equal(PipelineState.Unavailable, r.State));
        Assert.False(progress.SubmissionAvailable);
        Assert.False(progress.MeasuresAvailable);
        Assert.Equal(5, progress.Unavailable.Count);
        Assert.Null(progress.Measures.Single().Evaluated);
    }

    [Fact]
    public void Initial_population_comes_from_the_same_group_populations_as_the_populations_tab()
    {
        var map = ReportProgressService.InitialPopulationByType(
        [
            new ReportPopulationApiModel
            {
                ReportType = Ip,
                GroupPopulations =
                [
                    new GroupPopulationApiModel { PopulationId = "initial-population", TotalPopulationCount = 12 },
                    new GroupPopulationApiModel { PopulationId = "denominator", TotalPopulationCount = 9 }
                ]
            }
        ]);
        Assert.Equal(12, map[Ip]);
    }

    [Fact]
    public void Validation_stays_live_for_a_grace_window_after_submission()
    {
        FacilityReportRow Row(ScheduleStatus status, DateTime? submitted) => new() { Status = status, SubmittedUtc = submitted };
        Assert.False(ReportProgressService.ValidationSettled(Row(ScheduleStatus.EndOfPeriod, null), Now));
        Assert.False(ReportProgressService.ValidationSettled(Row(ScheduleStatus.Submitted, Now.AddMinutes(-1)), Now));
        Assert.True(ReportProgressService.ValidationSettled(Row(ScheduleStatus.Submitted, Now.AddMinutes(-4)), Now));
        Assert.True(ReportProgressService.ValidationSettled(null, Now));
    }

    [Fact]
    public void Per_measure_evaluated_is_blank_mid_run_when_several_measures_share_the_report()
    {
        var schedule = Schedule(ScheduleStatus.EndOfPeriod);
        schedule.ReportTypes = [Ip, "OtherMeasure"];
        var entries = Entries(identified: 4, pending: 0, passed: 6, submitted: 0);
        entries.ReportTypeCounts["OtherMeasure"] = 10;
        var progress = ReportProgressService.Build(schedule, entries, null, null, null, null, Now);
        Assert.All(progress.Measures, m => Assert.Equal(10, m.Patients));
        Assert.All(progress.Measures, m => Assert.Null(m.Evaluated));
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Link.UI")))
            dir = dir.Parent;
        return dir!.FullName;
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root(), "Link.UI", .. parts]));

    [Fact]
    public void Report_page_and_validation_update_live_and_stop_when_final()
    {
        var report = Read("Views", "Tenants", "Report.cshtml");
        Assert.DoesNotContain("<div class=\"card-header\">Patients</div>", report);
        Assert.Contains("_ReportLive", report);

        var live = Read("Views", "Tenants", "_ReportLive.cshtml");
        Assert.Contains("data-au-refresh=", live);
        Assert.Contains("data-au-final", live);

        var validation = Read("Views", "Reports", "Validation.cshtml");
        Assert.Contains("id=\"validation-live\"", validation);
        Assert.Contains("data-au-final", validation);

        var js = Read("wwwroot", "js", "live-region.js");
        Assert.Contains("fresh.hasAttribute(\"data-au-final\")", js);
        Assert.Contains("clearInterval(node._auTimer)", js);
    }
}