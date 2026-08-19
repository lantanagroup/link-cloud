using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Automation.Link.Validation;

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

    /// <summary>
    /// Backward-compatible overload that accepts a single expected measure ID.
    /// </summary>
    public Task ValidateAllAsync(
        string facilityId,
        string reportId,
        string expectedMeasureId,
        List<string> expectedPatientIds,
        Frequency expectedFrequency = Frequency.Adhoc,
        string? expectedAdHocType = "Manual",
        List<string>? expectedSubmittedPatientIds = null)
    {
        return ValidateAllAsync(
            facilityId, reportId, (IReadOnlyList<string>)[expectedMeasureId],
            expectedPatientIds, expectedFrequency, expectedAdHocType, expectedSubmittedPatientIds);
    }

    /// <summary>
    /// Validates the report database state after a pipeline run.
    /// </summary>
    /// <param name="manifest">
    /// Optional concrete generation manifest. When provided, the validator uses the
    /// known resource inventory and per-measure qualifying counts instead of assuming
    /// uniform expectations for all measures. When null, behaviour is backward-compatible.
    /// </param>
    public async Task ValidateAllAsync(
        string facilityId,
        string reportId,
        IReadOnlyList<string> expectedMeasureIds,
        List<string> expectedPatientIds,
        Frequency expectedFrequency = Frequency.Adhoc,
        string? expectedAdHocType = "Manual",
        List<string>? expectedSubmittedPatientIds = null,
        GenerationManifest? manifest = null)
    {
        var errors = new List<string>();

        // Derive qualifying count map from manifest when available.
        var qualifyingCountPerMeasure = manifest?.BuildQualifyingCountPerMeasure();

        try
        {
            var scheduleId = Guid.Parse(reportId);
            var expectedSubmitted = expectedSubmittedPatientIds ?? expectedPatientIds;

            await ValidateReportSchedule(scheduleId, facilityId, expectedFrequency, expectedAdHocType, errors);
            await ValidateScheduleReportTypes(scheduleId, expectedMeasureIds, errors);
            await ValidateReportEntries(scheduleId, facilityId, expectedPatientIds, expectedSubmitted, errors);
            await ValidateEntryMeasureReports(scheduleId, expectedMeasureIds, expectedPatientIds.Count, errors);
            var reportableMeasureTypeMap = await BuildHasReportableMeasureRowsByTypeAsync(scheduleId);
            await ValidateReportPopulations(scheduleId, facilityId, expectedMeasureIds, qualifyingCountPerMeasure, expectedSubmitted, reportableMeasureTypeMap, errors);
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

    private async Task ValidateScheduleReportTypes(Guid scheduleId, IReadOnlyList<string> expectedMeasureIds, List<string> errors)
    {
        var reportTypes = await _reader.GetScheduleReportTypesAsync(scheduleId);
        if (reportTypes.Count != expectedMeasureIds.Count)
        {
            AddError(errors, $"Expected {expectedMeasureIds.Count} ScheduleReportType row(s), found {reportTypes.Count}.");
            return;
        }

        var actualSet = reportTypes.Select(r => r.ReportType).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var expected in expectedMeasureIds)
        {
            if (!actualSet.Contains(expected))
                AddError(errors, $"ScheduleReportType missing expected measure: {expected}");
        }
    }

    private async Task ValidateReportEntries(
        Guid scheduleId,
        string facilityId,
        List<string> expectedPatientIds,
        List<string> expectedSubmittedPatientIds,
        List<string> errors)
    {
        var entries = await _reader.GetReportEntriesAsync(scheduleId);

        if (entries.Count != expectedPatientIds.Count)
            AddError(errors, $"ReportEntry count mismatch: expected {expectedPatientIds.Count}, actual {entries.Count}");

        var foundPatientIds = entries.Select(e => e.PatientId).OrderBy(p => p).ToList();
        var sortedExpected = expectedPatientIds.OrderBy(p => p).ToList();
        if (!foundPatientIds.SequenceEqual(sortedExpected))
            AddError(errors, $"ReportEntry patient IDs mismatch. expected=[{string.Join(",", sortedExpected)}], actual=[{string.Join(",", foundPatientIds)}]");

        var submittedSet = expectedSubmittedPatientIds.ToHashSet(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            if (entry.FacilityId != facilityId)
                AddError(errors, $"ReportEntry {entry.Id} FacilityId mismatch: expected {facilityId}, actual {entry.FacilityId}");

            if (submittedSet.Contains(entry.PatientId))
            {
                if (!string.Equals(entry.SubmissionStatus, "Submitted", StringComparison.OrdinalIgnoreCase))
                    AddError(errors, $"ReportEntry {entry.Id} for patient {entry.PatientId} should be Submitted, actual {entry.SubmissionStatus}");
            }
            else
            {
                var isNotEligible =
                    string.Equals(entry.SubmissionStatus, "NotEligable", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(entry.SubmissionStatus, "NotEligible", StringComparison.OrdinalIgnoreCase);

                if (!isNotEligible)
                    AddError(errors, $"ReportEntry {entry.Id} for non-submitted patient {entry.PatientId} should be NotEligible/NotEligable, actual {entry.SubmissionStatus}");
            }
        }
    }

    private async Task ValidateEntryMeasureReports(Guid scheduleId, IReadOnlyList<string> expectedMeasureIds, int expectedPatientCount, List<string> errors)
    {
        var reports = await _reader.GetEntryMeasureReportsAsync(scheduleId);
        var expectedTotal = expectedPatientCount * expectedMeasureIds.Count;

        if (reports.Count != expectedTotal)
            AddError(errors, $"EntryMeasureReport count mismatch: expected {expectedTotal} ({expectedPatientCount} patients x {expectedMeasureIds.Count} measures), actual {reports.Count}");

        var expectedSet = expectedMeasureIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var report in reports)
        {
            if (!string.IsNullOrWhiteSpace(report.ReportType) && !expectedSet.Contains(report.ReportType))
                AddError(errors, $"EntryMeasureReport {report.Id} ReportType '{report.ReportType}' does not match any expected measure [{string.Join(", ", expectedMeasureIds)}]");

            if (string.IsNullOrWhiteSpace(report.MeasureReportId))
                AddError(errors, $"EntryMeasureReport {report.Id} MeasureReportId should be populated.");
        }
    }

    private async Task ValidateReportPopulations(
        Guid scheduleId,
        string facilityId,
        IReadOnlyList<string> expectedMeasureIds,
        Dictionary<string, int>? expectedQualifyingCountPerMeasure,
        IReadOnlyList<string> expectedSubmittedPatientIds,
        IReadOnlyDictionary<string, bool> hasReportableRowsByReportType,
        List<string> errors)
    {
        var populations = await _reader.GetReportPopulationsAsync(scheduleId, facilityId);

        if (populations.Count == 0)
        {
            AddError(errors, "Expected at least one ReportPopulation row.");
            return;
        }

        var expectedSet = expectedMeasureIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var pop in populations)
        {
            if (!string.IsNullOrWhiteSpace(pop.ReportType) && !expectedSet.Contains(pop.ReportType))
                AddError(errors, $"ReportPopulation ReportType '{pop.ReportType}' does not match any expected measure [{string.Join(", ", expectedMeasureIds)}]");

            // Use cohort data to determine if this measure has any qualifying patients.
            var measureHasQualifyingPatients = true;

            if (!string.IsNullOrWhiteSpace(pop.ReportType)
                && hasReportableRowsByReportType.TryGetValue(pop.ReportType, out var hasReportableRows))
            {
                measureHasQualifyingPatients = hasReportableRows;
            }

            // expectedSubmittedPatientIds is the authoritative expectation produced by
            // run-planning prediction logic (profile expectations plus imported-patient
            // period-aware checks). When none are expected to be submitted,
            // ReportPopulation rows may be present but not contain valid
            // group/measure-report population rows.
            if (expectedSubmittedPatientIds.Count == 0)
                measureHasQualifyingPatients = false;

            if (expectedSubmittedPatientIds.Count > 0
                && expectedQualifyingCountPerMeasure != null
                && !string.IsNullOrWhiteSpace(pop.ReportType)
                && !hasReportableRowsByReportType.ContainsKey(pop.ReportType)
                && !string.IsNullOrWhiteSpace(pop.ReportType))
            {
                expectedQualifyingCountPerMeasure.TryGetValue(pop.ReportType, out var count);
                measureHasQualifyingPatients = count > 0;
            }

            if (pop.GroupPopulations.Count == 0)
            {
                if (!measureHasQualifyingPatients)
                {
                    _output.WriteLine($"  ReportPopulation '{pop.ReportType}' has no GroupPopulations (expected 0 qualifying patients per cohort config).");
                    continue;
                }

                AddError(errors, "ReportPopulation has no GroupPopulations.");
                continue;
            }

            var validGroups = pop.GroupPopulations.Count(gp =>
                !string.IsNullOrWhiteSpace(gp.PopulationCodeJson)
                && gp.PopulationCodeJson.Trim() != "{}"
                && gp.MeasureReportPopulations.Count > 0
                && gp.MeasureReportPopulations.All(mrp => !string.IsNullOrWhiteSpace(mrp.MeasureReportId)));

            if (validGroups == 0)
            {
                if (!measureHasQualifyingPatients)
                {
                    _output.WriteLine($"  ReportPopulation '{pop.ReportType}' has GroupPopulations but none are valid (expected 0 qualifying patients per cohort config).");
                    continue;
                }

                AddError(errors, $"ReportPopulation '{pop.ReportType}' has no valid GroupPopulation with population code and measure report rows.");
            }
        }
    }

    private async Task<IReadOnlyDictionary<string, bool>> BuildHasReportableMeasureRowsByTypeAsync(Guid scheduleId)
    {
        var rows = await _reader.GetEntryMeasureReportsAsync(scheduleId);

        return rows
            .Where(r => !string.IsNullOrWhiteSpace(r.ReportType))
            .GroupBy(r => r.ReportType!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Any(r => string.Equals(r.Status, "ReadyForValidation", StringComparison.OrdinalIgnoreCase)),
                StringComparer.OrdinalIgnoreCase);
    }
}
