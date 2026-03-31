using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Tests.E2ETests.Helpers;
using Xunit;

namespace LantanaGroup.Link.Tests.E2ETests.Validation;

/// <summary>
/// Validates the Report service's database state after a smoke test run.
/// Delegates all queries to <see cref="PipelineSnapshot"/> and applies strict assertions.
/// </summary>
public class ReportDatabaseValidator(DualOutputHelper output)
{
    public async Task ValidateAllAsync(
        string facilityId,
        string reportId,
        string expectedMeasureId,
        List<string> expectedPatientIds)
    {
        output.WriteLine("");
        output.WriteLine("=================================================================================");
        output.WriteLine("  REPORT DATABASE VALIDATION");
        output.WriteLine($"  FacilityId: {facilityId}");
        output.WriteLine($"  ReportId:   {reportId}");
        output.WriteLine("=================================================================================");

        var scheduleId = Guid.Parse(reportId);

        await using var db = DatabaseConnectionFactory.CreateReportDbContext();

        await ValidateReportSchedule(db, scheduleId, facilityId);
        await ValidateScheduleReportTypes(db, scheduleId, expectedMeasureId);
        await ValidateReportEntries(db, scheduleId, facilityId, expectedPatientIds);
        await ValidateEntryMeasureReports(db, scheduleId, expectedMeasureId, expectedPatientIds.Count);
        await ValidateReportResources(db, scheduleId, facilityId, expectedPatientIds);
        await ValidateReportPopulations(db, scheduleId, facilityId, expectedMeasureId);

        output.WriteLine("---------------------------------------------------------------------------------");
        output.WriteLine("  REPORT DATABASE VALIDATION COMPLETE");
        output.WriteLine("---------------------------------------------------------------------------------");
        output.WriteLine("");
    }

    private async Task ValidateReportSchedule(ReportDbContext db, Guid scheduleId, string facilityId)
    {
        output.WriteLine("");
        output.WriteLine("  --- ReportSchedule ---");

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

        output.WriteLine($"      FacilityId        = {schedule.FacilityId}");
        output.WriteLine($"      Frequency         = {schedule.Frequency}");
        output.WriteLine($"      AdHocType         = {schedule.AdHocType}");
        output.WriteLine($"      Status            = {schedule.Status}");
        output.WriteLine($"      ReportPeriod      = {schedule.ReportStartDate:O} to {schedule.ReportEndDate:O}");
        output.WriteLine("  --- ReportSchedule PASSED ---");
    }

    private async Task ValidateScheduleReportTypes(ReportDbContext db, Guid scheduleId, string expectedMeasureId)
    {
        output.WriteLine("");
        output.WriteLine("  --- ScheduleReportType ---");

        var reportTypes = await PipelineSnapshot.GetScheduleReportTypesAsync(db, scheduleId);

        Assert.Single(reportTypes);
        Assert.Equal(expectedMeasureId, reportTypes[0].ReportType);

        output.WriteLine($"      ReportType        = {reportTypes[0].ReportType}");
        output.WriteLine("  --- ScheduleReportType PASSED ---");
    }

    private async Task ValidateReportEntries(
        ReportDbContext db, Guid scheduleId, string facilityId, List<string> expectedPatientIds)
    {
        output.WriteLine("");
        output.WriteLine("  --- ReportEntry ---");

        var entries = await PipelineSnapshot.GetReportEntriesAsync(db, scheduleId);

        Assert.Equal(expectedPatientIds.Count, entries.Count);

        var foundPatientIds = entries.Select(e => e.PatientId).OrderBy(p => p).ToList();
        var sortedExpected = expectedPatientIds.OrderBy(p => p).ToList();
        Assert.Equal(sortedExpected, foundPatientIds);

        foreach (var entry in entries)
        {
            Assert.Equal(facilityId, entry.FacilityId);
            Assert.Equal(SubmissionStatus.Submitted, entry.SubmissionStatus);

            output.WriteLine($"      Patient {entry.PatientId,-12} ReportingStatus={entry.ReportingStatus}, SubmissionStatus={entry.SubmissionStatus}");
        }

        output.WriteLine("  --- ReportEntry PASSED ---");
    }

    private async Task ValidateEntryMeasureReports(
        ReportDbContext db, Guid scheduleId, string expectedMeasureId, int expectedPatientCount)
    {
        output.WriteLine("");
        output.WriteLine("  --- EntryMeasureReport ---");

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

            output.WriteLine($"      Id={report.Id}");
            output.WriteLine($"        Type            = {report.ReportType}");
            output.WriteLine($"        Status          = {report.Status}");
            output.WriteLine($"        MeasureReportId = {report.MeasureReportId}");
            output.WriteLine($"        ResourceCounts  = [{resourceCountSummary}]");
        }

        output.WriteLine("  --- EntryMeasureReport PASSED ---");
    }

    private async Task ValidateReportResources(
        ReportDbContext db, Guid scheduleId, string facilityId, List<string> expectedPatientIds)
    {
        output.WriteLine("");
        output.WriteLine("  --- ReportResource ---");

        var resources = await PipelineSnapshot.GetReportResourceSummaryAsync(db, scheduleId, facilityId);
        var patientsWithResources = resources.Select(r => r.PatientId).Distinct().ToList();

        foreach (var patientId in expectedPatientIds)
        {
            Assert.Contains(patientId, patientsWithResources);

            var patientResources = resources.Where(r => r.PatientId == patientId).ToList();
            var totalCount = patientResources.Sum(r => r.Count);
            output.WriteLine($"      Patient {patientId,-12} {totalCount} resources across {patientResources.Count} types");
        }

        output.WriteLine("  --- ReportResource PASSED ---");
    }

    private async Task ValidateReportPopulations(
        ReportDbContext db, Guid scheduleId, string facilityId, string expectedMeasureId)
    {
        output.WriteLine("");
        output.WriteLine("  --- ReportPopulation ---");

        var populations = await PipelineSnapshot.GetReportPopulationsAsync(db, scheduleId, facilityId);

        Assert.True(populations.Count > 0, "Expected at least one ReportPopulation row");

        foreach (var pop in populations)
        {
            Assert.Equal(expectedMeasureId, pop.ReportType);
            output.WriteLine($"      Population Id     = {pop.Id}");
            output.WriteLine($"        Measure         = {pop.Measure}");
            output.WriteLine($"        ReportType      = {pop.ReportType}");

            Assert.True(pop.GroupPopulations.Count > 0,
                $"Expected GroupPopulations for ReportPopulation Id={pop.Id}");

            foreach (var gp in pop.GroupPopulations)
            {
                Assert.False(string.IsNullOrWhiteSpace(gp.PopulationCodeJson),
                    $"PopulationCodeJson should not be empty for GroupPopulation Id={gp.Id}");
                Assert.NotEqual("{}", gp.PopulationCodeJson.Trim());

                Assert.True(gp.MeasureReportPopulations.Count > 0,
                    $"Expected at least one MeasureReportPopulation for GroupPopulation Id={gp.Id} (PopulationId={gp.PopulationId})");

                output.WriteLine($"        GroupPopulation Id={gp.Id}");
                output.WriteLine($"          PopulationId       = {gp.PopulationId}");
                output.WriteLine($"          TotalCount         = {gp.TotalPopulationCount}");
                output.WriteLine($"          PopulationCodeJson = {gp.PopulationCodeJson}");

                foreach (var mrp in gp.MeasureReportPopulations)
                {
                    Assert.False(string.IsNullOrWhiteSpace(mrp.MeasureReportId),
                        $"MeasureReportId should be set on MeasureReportPopulation Id={mrp.Id}");

                    output.WriteLine($"          MeasureReportPopulation Id={mrp.Id}");
                    output.WriteLine($"            MeasureReportId  = {mrp.MeasureReportId}");
                    output.WriteLine($"            PopulationCount  = {mrp.PopulationCount}");
                }
            }
        }

        output.WriteLine("  --- ReportPopulation PASSED ---");
    }
}

