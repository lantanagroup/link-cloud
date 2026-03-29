using LantanaGroup.Link.Automation.Helpers;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Automation.Validation;

public class ReportDatabaseValidator
{
    private const int MaxErrors = 100;
    private readonly IAutomationOutput _output;
    private readonly PipelineDataReader _reader;

    public ReportDatabaseValidator(IAutomationOutput output, PipelineDataReader reader)
    {
        _output = output;
        _reader = reader;
    }

    public async Task ValidateAllAsync(
        string facilityId,
        string reportId,
        string expectedMeasureId,
        List<string> expectedPatientIds,
        Frequency expectedFrequency = Frequency.Adhoc,
        string? expectedAdHocType = "Manual")
    {
        var errors = new List<string>();

        try
        {
            var scheduleId = Guid.Parse(reportId);

            await ValidateReportSchedule(scheduleId, facilityId, expectedFrequency, expectedAdHocType, errors);
            await ValidateScheduleReportTypes(scheduleId, expectedMeasureId, errors);
            await ValidateReportEntries(scheduleId, facilityId, expectedPatientIds, errors);
            await ValidateEntryMeasureReports(scheduleId, expectedMeasureId, expectedPatientIds.Count, errors);
            await ValidateReportResources(scheduleId, facilityId, expectedPatientIds, errors);
            await ValidateReportPopulations(scheduleId, facilityId, expectedMeasureId, errors);
        }
        catch (Exception ex)
        {
            AddError(errors, $"Unhandled exception during report DB validation: {ex.Message}");
        }

        if (errors.Count == 0)
        {
            _output.WriteLine("REPORT DATABASE VALIDATION: Passed");
            return;
        }

        _output.WriteLine($"REPORT DATABASE VALIDATION: Failed ({errors.Count} issue(s))");
        foreach (var error in errors)
        {
            _output.WriteLine($"  - {error}");
        }

        throw new InvalidOperationException($"REPORT DATABASE VALIDATION failed with {errors.Count} issue(s).");
    }

    private static void AddError(List<string> errors, string message)
    {
        if (errors.Count < MaxErrors)
            errors.Add(message);
    }

    private async Task ValidateReportSchedule(Guid scheduleId, string facilityId, Frequency expectedFrequency, string? expectedAdHocType, List<string> errors)
    {
        var schedule = await _reader.GetReportScheduleAsync(scheduleId);
        if (schedule == null)
        {
            AddError(errors, "ReportSchedule row not found.");
            return;
        }

        if (schedule.FacilityId != facilityId) AddError(errors, $"ReportSchedule.FacilityId mismatch: expected {facilityId}, actual {schedule.FacilityId}");
        if (!string.Equals(schedule.Frequency, expectedFrequency.ToString(), StringComparison.OrdinalIgnoreCase)) AddError(errors, $"ReportSchedule.Frequency mismatch: expected {expectedFrequency}, actual {schedule.Frequency}");

        if (expectedAdHocType == null)
        {
            if (!string.IsNullOrWhiteSpace(schedule.AdHocType))
                AddError(errors, $"ReportSchedule.AdHocType mismatch: expected null/empty, actual {schedule.AdHocType}");
        }
        else if (!string.Equals(schedule.AdHocType, expectedAdHocType, StringComparison.OrdinalIgnoreCase))
        {
            AddError(errors, $"ReportSchedule.AdHocType mismatch: expected {expectedAdHocType}, actual {schedule.AdHocType}");
        }

        if (!string.Equals(schedule.Status, ScheduleStatus.Submitted.ToString(), StringComparison.OrdinalIgnoreCase)) AddError(errors, $"ReportSchedule.Status mismatch: expected {ScheduleStatus.Submitted}, actual {schedule.Status}");
        if (!schedule.EnableSubmission) AddError(errors, "ReportSchedule.EnableSubmission should be true.");
        if (!schedule.EndOfReportPeriodJobHasRun) AddError(errors, "ReportSchedule.EndOfReportPeriodJobHasRun should be true.");
        if (string.IsNullOrWhiteSpace(schedule.PayloadRootUri)) AddError(errors, "ReportSchedule.PayloadRootUri should be populated.");
        if (schedule.ReportStartDate >= schedule.ReportEndDate) AddError(errors, "ReportSchedule.ReportStartDate must be before ReportEndDate.");
    }

    private async Task ValidateScheduleReportTypes(Guid scheduleId, string expectedMeasureId, List<string> errors)
    {
        var reportTypes = await _reader.GetScheduleReportTypesAsync(scheduleId);
        if (reportTypes.Count != 1)
        {
            AddError(errors, $"Expected exactly 1 ScheduleReportType row, found {reportTypes.Count}.");
            return;
        }

        if (reportTypes[0].ReportType != expectedMeasureId)
            AddError(errors, $"ScheduleReportType.ReportType mismatch: expected {expectedMeasureId}, actual {reportTypes[0].ReportType}");
    }

    private async Task ValidateReportEntries(Guid scheduleId, string facilityId, List<string> expectedPatientIds, List<string> errors)
    {
        var entries = await _reader.GetReportEntriesAsync(scheduleId);

        if (entries.Count != expectedPatientIds.Count)
            AddError(errors, $"ReportEntry count mismatch: expected {expectedPatientIds.Count}, actual {entries.Count}");

        var foundPatientIds = entries.Select(e => e.PatientId).OrderBy(p => p).ToList();
        var sortedExpected = expectedPatientIds.OrderBy(p => p).ToList();
        if (!foundPatientIds.SequenceEqual(sortedExpected))
            AddError(errors, $"ReportEntry patient IDs mismatch. expected=[{string.Join(",", sortedExpected)}], actual=[{string.Join(",", foundPatientIds)}]");

        foreach (var entry in entries)
        {
            if (entry.FacilityId != facilityId)
                AddError(errors, $"ReportEntry {entry.Id} FacilityId mismatch: expected {facilityId}, actual {entry.FacilityId}");

            if (!string.Equals(entry.SubmissionStatus, "Submitted", StringComparison.OrdinalIgnoreCase))
                AddError(errors, $"ReportEntry {entry.Id} SubmissionStatus should be Submitted, actual {entry.SubmissionStatus}");
        }
    }

    private async Task ValidateEntryMeasureReports(Guid scheduleId, string expectedMeasureId, int expectedPatientCount, List<string> errors)
    {
        var reports = await _reader.GetEntryMeasureReportsAsync(scheduleId);

        if (reports.Count != expectedPatientCount)
            AddError(errors, $"EntryMeasureReport count mismatch: expected {expectedPatientCount}, actual {reports.Count}");

        foreach (var report in reports)
        {
            if (report.ReportType != expectedMeasureId)
                AddError(errors, $"EntryMeasureReport {report.Id} ReportType mismatch: expected {expectedMeasureId}, actual {report.ReportType}");

            if (string.IsNullOrWhiteSpace(report.MeasureReportId))
                AddError(errors, $"EntryMeasureReport {report.Id} MeasureReportId should be populated.");
        }
    }

    private async Task ValidateReportResources(Guid scheduleId, string facilityId, List<string> expectedPatientIds, List<string> errors)
    {
        var resources = await _reader.GetReportResourceSummaryAsync(scheduleId, facilityId);
        var patientsWithResources = resources.Select(r => r.PatientId).Distinct().ToHashSet();

        foreach (var patientId in expectedPatientIds)
        {
            if (!patientsWithResources.Contains(patientId))
                AddError(errors, $"No ReportResource rows found for expected patient {patientId}.");
        }
    }

    private async Task ValidateReportPopulations(Guid scheduleId, string facilityId, string expectedMeasureId, List<string> errors)
    {
        var populations = await _reader.GetReportPopulationsAsync(scheduleId, facilityId);

        if (populations.Count == 0)
        {
            AddError(errors, "Expected at least one ReportPopulation row.");
            return;
        }

        foreach (var pop in populations)
        {
            if (pop.ReportType != expectedMeasureId)
                AddError(errors, $"ReportPopulation ReportType mismatch: expected {expectedMeasureId}, actual {pop.ReportType}");

            if (pop.GroupPopulations.Count == 0)
            {
                AddError(errors, "ReportPopulation has no GroupPopulations.");
                continue;
            }

            foreach (var gp in pop.GroupPopulations)
            {
                if (string.IsNullOrWhiteSpace(gp.PopulationCodeJson) || gp.PopulationCodeJson.Trim() == "{}")
                    AddError(errors, "GroupPopulation PopulationCodeJson is empty/invalid.");

                if (gp.MeasureReportPopulations.Count == 0)
                    AddError(errors, "GroupPopulation has no MeasureReportPopulation rows.");

                foreach (var mrp in gp.MeasureReportPopulations)
                {
                    if (string.IsNullOrWhiteSpace(mrp.MeasureReportId))
                        AddError(errors, "MeasureReportPopulation MeasureReportId should be populated.");
                }
            }
        }
    }
}
