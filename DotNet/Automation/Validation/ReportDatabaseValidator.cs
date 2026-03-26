using LantanaGroup.Link.Automation.Helpers;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using Xunit;
using Xunit.Abstractions;

namespace LantanaGroup.Link.Automation.Validation;

/// <summary>
/// Validates the Report service's database state after a smoke test run.
/// </summary>
public class ReportDatabaseValidator
{
    private readonly ITestOutputHelper _output;
    private readonly DatabaseConnectionFactory _dbFactory;

    public ReportDatabaseValidator(ITestOutputHelper output, DatabaseConnectionFactory dbFactory)
    {
        _output = output;
        _dbFactory = dbFactory;
    }

    public async Task ValidateAllAsync(
        string facilityId,
        string reportId,
        string expectedMeasureId,
        List<string> expectedPatientIds)
    {
        _output.WriteLine("");
        _output.WriteLine("=================================================================================");
        _output.WriteLine("  REPORT DATABASE VALIDATION");
        _output.WriteLine($"  FacilityId: {facilityId}");
        _output.WriteLine($"  ReportId:   {reportId}");
        _output.WriteLine("=================================================================================");

        var scheduleId = Guid.Parse(reportId);

        await using var db = _dbFactory.CreateReportDbContext();

        await ValidateReportSchedule(db, scheduleId, facilityId);
        await ValidateScheduleReportTypes(db, scheduleId, expectedMeasureId);
        await ValidateReportEntries(db, scheduleId, facilityId, expectedPatientIds);
        await ValidateEntryMeasureReports(db, scheduleId, expectedMeasureId, expectedPatientIds.Count);
        await ValidateReportResources(db, scheduleId, facilityId, expectedPatientIds);
        await ValidateReportPopulations(db, scheduleId, facilityId, expectedMeasureId);

        _output.WriteLine("---------------------------------------------------------------------------------");
        _output.WriteLine("  REPORT DATABASE VALIDATION COMPLETE");
        _output.WriteLine("---------------------------------------------------------------------------------");
        _output.WriteLine("");
    }

    private async Task ValidateReportSchedule(ReportDbContext db, Guid scheduleId, string facilityId)
    {
        _output.WriteLine("");
        _output.WriteLine("  --- ReportSchedule ---");

        var schedule = await PipelineSnapshot.GetReportScheduleAsync(db, scheduleId);

        Assert.NotNull(schedule);
        Assert.Equal(facilityId, schedule.FacilityId);
        Assert.Equal(Frequency.Adhoc, schedule.Frequency);
        Assert.Equal(AdHocType.Manual, schedule.AdHocType);
        Assert.Equal(ScheduleStatus.Submitted, schedule.Status);
        Assert.True(schedule.EnableSubmission, "EnableSubmission should be true (BypassSubmission=false)");
        Assert.True(schedule.EndOfReportPeriodJobHasRun, "EndOfReportPeriodJobHasRun should be true for ad-hoc reports");
        Assert.False(string.IsNullOrWhiteSpace(schedule.PayloadRootUri), "PayloadRootUri should be set");
        Assert.True(schedule.ReportStartDate < schedule.ReportEndDate, "StartDate should be before EndDate");

        _output.WriteLine($"      FacilityId        = {schedule.FacilityId}");
        _output.WriteLine($"      Frequency         = {schedule.Frequency}");
        _output.WriteLine($"      AdHocType         = {schedule.AdHocType}");
        _output.WriteLine($"      Status            = {schedule.Status}");
        _output.WriteLine($"      ReportPeriod      = {schedule.ReportStartDate:O} to {schedule.ReportEndDate:O}");
        _output.WriteLine("  --- ReportSchedule PASSED ---");
    }

    private async Task ValidateScheduleReportTypes(ReportDbContext db, Guid scheduleId, string expectedMeasureId)
    {
        _output.WriteLine("");
        _output.WriteLine("  --- ScheduleReportType ---");

        var reportTypes = await PipelineSnapshot.GetScheduleReportTypesAsync(db, scheduleId);

        Assert.Single(reportTypes);
        Assert.Equal(expectedMeasureId, reportTypes[0].ReportType);

        _output.WriteLine($"      ReportType        = {reportTypes[0].ReportType}");
        _output.WriteLine("  --- ScheduleReportType PASSED ---");
    }

    private async Task ValidateReportEntries(
        ReportDbContext db, Guid scheduleId, string facilityId, List<string> expectedPatientIds)
    {
        _output.WriteLine("");
        _output.WriteLine("  --- ReportEntry ---");

        var entries = await PipelineSnapshot.GetReportEntriesAsync(db, scheduleId);

        Assert.Equal(expectedPatientIds.Count, entries.Count);

        var foundPatientIds = entries.Select(e => e.PatientId).OrderBy(p => p).ToList();
        var sortedExpected = expectedPatientIds.OrderBy(p => p).ToList();
        Assert.Equal(sortedExpected, foundPatientIds);

        foreach (var entry in entries)
        {
            Assert.Equal(facilityId, entry.FacilityId);
            Assert.Equal(SubmissionStatus.Submitted, entry.SubmissionStatus);

            _output.WriteLine($"      Patient {entry.PatientId,-12} ReportingStatus={entry.ReportingStatus}, SubmissionStatus={entry.SubmissionStatus}");
        }

        _output.WriteLine("  --- ReportEntry PASSED ---");
    }

    private async Task ValidateEntryMeasureReports(
        ReportDbContext db, Guid scheduleId, string expectedMeasureId, int expectedPatientCount)
    {
        _output.WriteLine("");
        _output.WriteLine("  --- EntryMeasureReport ---");

        var reports = await PipelineSnapshot.GetEntryMeasureReportsAsync(db, scheduleId);

        Assert.Equal(expectedPatientCount, reports.Count);

        foreach (var report in reports)
        {
            Assert.Equal(expectedMeasureId, report.ReportType);
            Assert.False(string.IsNullOrWhiteSpace(report.MeasureReportId),
                $"MeasureReportId should be populated for EntryMeasureReport Id={report.Id}");

            var resourceCountSummary = report.ResourceCounts.Any()
                ? string.Join(", ", report.ResourceCounts.Select(rc => $"{rc.ResourceType}={rc.ResourceCount}"))
                : "(none)";

            _output.WriteLine($"      Id={report.Id}");
            _output.WriteLine($"        Type            = {report.ReportType}");
            _output.WriteLine($"        Status          = {report.Status}");
            _output.WriteLine($"        MeasureReportId = {report.MeasureReportId}");
            _output.WriteLine($"        ResourceCounts  = [{resourceCountSummary}]");
        }

        _output.WriteLine("  --- EntryMeasureReport PASSED ---");
    }

    private async Task ValidateReportResources(
        ReportDbContext db, Guid scheduleId, string facilityId, List<string> expectedPatientIds)
    {
        _output.WriteLine("");
        _output.WriteLine("  --- ReportResource ---");

        var resources = await PipelineSnapshot.GetReportResourceSummaryAsync(db, scheduleId, facilityId);
        var patientsWithResources = resources.Select(r => r.PatientId).Distinct().ToList();

        foreach (var patientId in expectedPatientIds)
        {
            Assert.Contains(patientId, patientsWithResources);

            var patientResources = resources.Where(r => r.PatientId == patientId).ToList();
            var totalCount = patientResources.Sum(r => r.Count);
            _output.WriteLine($"      Patient {patientId,-12} {totalCount} resources across {patientResources.Count} types");
        }

        _output.WriteLine("  --- ReportResource PASSED ---");
    }

    private async Task ValidateReportPopulations(
        ReportDbContext db, Guid scheduleId, string facilityId, string expectedMeasureId)
    {
        _output.WriteLine("");
        _output.WriteLine("  --- ReportPopulation ---");

        var populations = await PipelineSnapshot.GetReportPopulationsAsync(db, scheduleId, facilityId);

        Assert.True(populations.Count > 0, "Expected at least one ReportPopulation row");

        foreach (var pop in populations)
        {
            Assert.Equal(expectedMeasureId, pop.ReportType);
            _output.WriteLine($"      Population Id     = {pop.Id}");
            _output.WriteLine($"        Measure         = {pop.Measure}");
            _output.WriteLine($"        ReportType      = {pop.ReportType}");

            Assert.True(pop.GroupPopulations.Count > 0,
                $"Expected GroupPopulations for ReportPopulation Id={pop.Id}");

            foreach (var gp in pop.GroupPopulations)
            {
                Assert.False(string.IsNullOrWhiteSpace(gp.PopulationCodeJson),
                    $"PopulationCodeJson should not be empty for GroupPopulation Id={gp.Id}");
                Assert.NotEqual("{}", gp.PopulationCodeJson.Trim());

                Assert.True(gp.MeasureReportPopulations.Count > 0,
                    $"Expected at least one MeasureReportPopulation for GroupPopulation Id={gp.Id} (PopulationId={gp.PopulationId})");

                _output.WriteLine($"        GroupPopulation Id={gp.Id}");
                _output.WriteLine($"          PopulationId       = {gp.PopulationId}");
                _output.WriteLine($"          TotalCount         = {gp.TotalPopulationCount}");
                _output.WriteLine($"          PopulationCodeJson = {gp.PopulationCodeJson}");

                foreach (var mrp in gp.MeasureReportPopulations)
                {
                    Assert.False(string.IsNullOrWhiteSpace(mrp.MeasureReportId),
                        $"MeasureReportId should be set on MeasureReportPopulation Id={mrp.Id}");

                    _output.WriteLine($"          MeasureReportPopulation Id={mrp.Id}");
                    _output.WriteLine($"            MeasureReportId  = {mrp.MeasureReportId}");
                    _output.WriteLine($"            PopulationCount  = {mrp.PopulationCount}");
                }
            }
        }

        _output.WriteLine("  --- ReportPopulation PASSED ---");
    }
}
