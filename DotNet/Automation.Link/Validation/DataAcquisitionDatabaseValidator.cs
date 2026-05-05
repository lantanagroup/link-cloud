using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Automation.Link.Validation;

public class DataAcquisitionDatabaseValidator
{
    private const int MaxErrors = 100;
    private readonly IAutomationOutput _output;
    private readonly PipelineDataReader _reader;

    public DataAcquisitionDatabaseValidator(IAutomationOutput output, PipelineDataReader reader)
    {
        _output = output;
        _reader = reader;
    }

    public async Task ValidateAllAsync(
        string facilityId,
        string reportId,
        string expectedMeasureId,
        List<string> expectedPatientIds,
        bool expectDataAcquisitionData = true)
    {
        var errors = new List<string>();

        try
        {
            await ValidateFhirQueryConfiguration(facilityId, errors);
            await ValidateQueryPlans(facilityId, expectedMeasureId, errors);
            await ValidateDataAcquisitionLogs(facilityId, reportId, expectedPatientIds, errors, expectDataAcquisitionData);
            await ValidateFhirQueries(facilityId, reportId, errors, expectDataAcquisitionData);
            await ValidateReferenceResources(facilityId, reportId, errors, expectDataAcquisitionData);
        }
        catch (Exception ex)
        {
            AddError(errors, $"Unhandled exception during data acquisition DB validation: {ex.Message}");
        }

        if (errors.Count == 0)
        {
            _output.WriteLine("DATA ACQUISITION DATABASE VALIDATION: Passed");
            return;
        }

        _output.WriteLine($"DATA ACQUISITION DATABASE VALIDATION: Failed ({errors.Count} issue(s))");
        foreach (var error in errors)
        {
            _output.WriteLine($"  - {error}");
        }

        throw new InvalidOperationException($"DATA ACQUISITION DATABASE VALIDATION failed with {errors.Count} issue(s).");
    }

    private static void AddError(List<string> errors, string message)
    {
        if (errors.Count < MaxErrors)
            errors.Add(message);
    }

    private async Task ValidateFhirQueryConfiguration(string facilityId, List<string> errors)
    {
        var hasConfig = await _reader.HasFhirQueryConfigurationAsync(facilityId);
        if (!hasConfig)
            AddError(errors, "FhirQueryConfiguration not found.");
    }

    private async Task ValidateQueryPlans(string facilityId, string expectedMeasureId, List<string> errors)
    {
        var queryPlans = await _reader.GetQueryPlansAsync(facilityId);

        if (queryPlans.Count < 2)
            AddError(errors, $"Expected at least 2 query plans (Discharge + Monthly), found {queryPlans.Count}.");

        var dischargePlan = queryPlans.FirstOrDefault(qp => string.Equals(qp.Type, Frequency.Discharge.ToString(), StringComparison.OrdinalIgnoreCase));
        if (dischargePlan == null)
        {
            AddError(errors, "Discharge query plan not found.");
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(dischargePlan.PlanName) && dischargePlan.PlanName != expectedMeasureId) AddError(errors, $"Discharge plan name mismatch: expected {expectedMeasureId}, actual {dischargePlan.PlanName}");
            if (dischargePlan.InitialQueriesCount <= 0) AddError(errors, "Discharge plan InitialQueries should be populated.");
            if (dischargePlan.SupplementalQueriesCount <= 0) AddError(errors, "Discharge plan SupplementalQueries should be populated.");
        }

        var monthlyPlan = queryPlans.FirstOrDefault(qp => string.Equals(qp.Type, Frequency.Monthly.ToString(), StringComparison.OrdinalIgnoreCase));
        if (monthlyPlan == null)
        {
            AddError(errors, "Monthly query plan not found.");
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(monthlyPlan.PlanName) && monthlyPlan.PlanName != expectedMeasureId) AddError(errors, $"Monthly plan name mismatch: expected {expectedMeasureId}, actual {monthlyPlan.PlanName}");
            if (monthlyPlan.InitialQueriesCount <= 0) AddError(errors, "Monthly plan InitialQueries should be populated.");
            if (monthlyPlan.SupplementalQueriesCount <= 0) AddError(errors, "Monthly plan SupplementalQueries should be populated.");
        }
    }

    private async Task ValidateDataAcquisitionLogs(string facilityId, string reportId, List<string> expectedPatientIds, List<string> errors, bool expectDataAcquisitionData)
    {
        var logs = await _reader.GetAcquisitionLogsAsync(facilityId, reportId);

        if (!expectDataAcquisitionData)
        {
            if (logs.Count > 0)
                AddError(errors, $"Expected no DataAcquisitionLog rows for report {reportId} but found {logs.Count}.");
            return;
        }

        if (logs.Count == 0)
        {
            AddError(errors, $"Expected DataAcquisitionLog rows for report {reportId} but found none.");
            return;
        }

        var patientsInLogs = logs
            .Where(l => l.PatientId != null)
            .Select(l => l.PatientId!)
            .Distinct()
            .ToHashSet();

        foreach (var patientId in expectedPatientIds)
        {
            if (!patientsInLogs.Contains(patientId))
                AddError(errors, $"No DataAcquisitionLog rows found for expected patient {patientId}.");
        }

        var failedLogs = logs.Where(l => string.Equals(l.Status, "Failed", StringComparison.OrdinalIgnoreCase) || string.Equals(l.Status, "MaxRetriesReached", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var failed in failedLogs.Take(10))
        {
            AddError(errors, $"Failed acquisition log: Id={failed.Id}, Patient={failed.PatientId}, Status={failed.Status}");
        }

        if (failedLogs.Count > 10)
            AddError(errors, $"Additional failed acquisition logs omitted: {failedLogs.Count - 10}");
    }

    private async Task ValidateFhirQueries(string facilityId, string reportId, List<string> errors, bool expectDataAcquisitionData)
    {
        var hasFhirQueries = await _reader.HasAnyFhirQueryRowsForReportAsync(facilityId, reportId);

        if (!expectDataAcquisitionData)
        {
            if (hasFhirQueries)
                AddError(errors, $"Expected no FhirQuery rows for report {reportId} but found at least one.");
            return;
        }

        if (!hasFhirQueries)
            AddError(errors, "Expected FhirQuery rows for this report but found none.");
    }

    private async Task ValidateReferenceResources(string facilityId, string reportId, List<string> errors, bool expectDataAcquisitionData)
    {
        var logs = await _reader.GetAcquisitionLogsAsync(facilityId, reportId);
        var hasReferenceResources = await _reader.HasAnyReferenceResourcesForReportAsync(facilityId, reportId);

        if (!expectDataAcquisitionData)
        {
            if (hasReferenceResources)
                AddError(errors, $"Expected no ReferenceResources rows for report {reportId} but found at least one.");
            return;
        }

        if (hasReferenceResources)
            return;

        var referentialLogs = logs
            .Where(l => string.Equals(l.QueryPhase, "Referential", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var referentialCompleted = referentialLogs.Count(l =>
            string.Equals(l.Status, "Completed", StringComparison.OrdinalIgnoreCase));

        // If no referential work completed, reference-resource rows are not expected.
        if (referentialCompleted == 0)
            return;

        // Small consistency wait if referential work is already marked completed.
        if (referentialCompleted > 0)
        {
            for (var i = 0; i < 10; i++)
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
                hasReferenceResources = await _reader.HasAnyReferenceResourcesForReportAsync(facilityId, reportId);
                if (hasReferenceResources)
                    return;
            }
        }

        var statusSummary = referentialLogs
            .GroupBy(l => l.Status ?? "(null)", StringComparer.OrdinalIgnoreCase)
            .Select(g => $"{g.Key}={g.Count()}")
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);

        AddError(
            errors,
            $"Expected ReferenceResources rows for the facility but found none. " +
            $"Diagnostics: totalLogs={logs.Count}, referentialLogs={referentialLogs.Count}, referentialCompleted={referentialCompleted}, " +
            $"referentialStatuses=[{string.Join(", ", statusSummary)}].");
    }
}
