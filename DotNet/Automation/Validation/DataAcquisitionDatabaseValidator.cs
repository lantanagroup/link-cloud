using LantanaGroup.Link.Automation.Helpers;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Automation.Validation;

public class DataAcquisitionDatabaseValidator
{
    private const int MaxErrors = 100;
    private readonly IAutomationOutput _output;
    private readonly PipelineDataReader _reader;

    public DataAcquisitionDatabaseValidator(IAutomationOutput output, DatabaseConnectionFactory dbFactory)
    {
        _output = output;
        _reader = new PipelineDataReader(dbFactory);
    }

    public async Task ValidateAllAsync(
        string facilityId,
        string reportId,
        string expectedMeasureId,
        List<string> expectedPatientIds)
    {
        var errors = new List<string>();

        try
        {
            await ValidateFhirQueryConfiguration(facilityId, errors);
            await ValidateQueryPlans(facilityId, expectedMeasureId, errors);
            await ValidateDataAcquisitionLogs(facilityId, reportId, expectedPatientIds, errors);
            await ValidateFhirQueries(facilityId, reportId, errors);
            await ValidateReferenceResources(facilityId, errors);
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

        var dischargePlan = queryPlans.FirstOrDefault(qp => qp.Type == Frequency.Discharge);
        if (dischargePlan == null)
        {
            AddError(errors, "Discharge query plan not found.");
        }
        else
        {
            if (dischargePlan.PlanName != expectedMeasureId) AddError(errors, $"Discharge plan name mismatch: expected {expectedMeasureId}, actual {dischargePlan.PlanName}");
            if (dischargePlan.InitialQueries?.Count <= 0) AddError(errors, "Discharge plan InitialQueries should be populated.");
            if (dischargePlan.SupplementalQueries?.Count <= 0) AddError(errors, "Discharge plan SupplementalQueries should be populated.");
        }

        var monthlyPlan = queryPlans.FirstOrDefault(qp => qp.Type == Frequency.Monthly);
        if (monthlyPlan == null)
        {
            AddError(errors, "Monthly query plan not found.");
        }
        else
        {
            if (monthlyPlan.PlanName != expectedMeasureId) AddError(errors, $"Monthly plan name mismatch: expected {expectedMeasureId}, actual {monthlyPlan.PlanName}");
            if (monthlyPlan.InitialQueries?.Count <= 0) AddError(errors, "Monthly plan InitialQueries should be populated.");
            if (monthlyPlan.SupplementalQueries?.Count <= 0) AddError(errors, "Monthly plan SupplementalQueries should be populated.");
        }
    }

    private async Task ValidateDataAcquisitionLogs(string facilityId, string reportId, List<string> expectedPatientIds, List<string> errors)
    {
        var logs = await _reader.GetAcquisitionLogsAsync(facilityId, reportId);
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

        var failedLogs = logs.Where(l => l.Status == RequestStatus.Failed || l.Status == RequestStatus.MaxRetriesReached).ToList();
        foreach (var failed in failedLogs.Take(10))
        {
            AddError(errors, $"Failed acquisition log: Id={failed.Id}, Patient={failed.PatientId}, Status={failed.Status}");
        }

        if (failedLogs.Count > 10)
            AddError(errors, $"Additional failed acquisition logs omitted: {failedLogs.Count - 10}");
    }

    private async Task ValidateFhirQueries(string facilityId, string reportId, List<string> errors)
    {
        var queries = await _reader.GetFhirQueriesForReportAsync(facilityId, reportId);
        if (queries.Count == 0)
            AddError(errors, "Expected FhirQuery rows for this report but found none.");
    }

    private async Task ValidateReferenceResources(string facilityId, List<string> errors)
    {
        var groupCount = await _reader.GetReferenceResourceGroupCountAsync(facilityId);
        if (groupCount == 0)
            AddError(errors, "Expected ReferenceResources rows for the facility but found none.");
    }
}
