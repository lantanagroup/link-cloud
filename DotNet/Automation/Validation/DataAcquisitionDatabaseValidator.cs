using LantanaGroup.Link.Automation.Helpers;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace LantanaGroup.Link.Automation.Validation;

/// <summary>
/// Validates the DataAcquisition service's database state after a smoke test run.
/// </summary>
public class DataAcquisitionDatabaseValidator
{
    private readonly ITestOutputHelper _output;
    private readonly DatabaseConnectionFactory _dbFactory;

    public DataAcquisitionDatabaseValidator(ITestOutputHelper output, DatabaseConnectionFactory dbFactory)
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
        _output.WriteLine("  DATA ACQUISITION DATABASE VALIDATION");
        _output.WriteLine($"  FacilityId: {facilityId}");
        _output.WriteLine($"  ReportId:   {reportId}");
        _output.WriteLine("=================================================================================");

        await using var db = _dbFactory.CreateDataAcquisitionDbContext();

        await ValidateFhirQueryConfiguration(db, facilityId);
        await ValidateQueryPlans(db, facilityId, expectedMeasureId);
        await ValidateDataAcquisitionLogs(db, facilityId, reportId, expectedPatientIds);
        await ValidateFhirQueries(db, facilityId, reportId);
        await ValidateReferenceResources(db, facilityId);

        _output.WriteLine("---------------------------------------------------------------------------------");
        _output.WriteLine("  DATA ACQUISITION DATABASE VALIDATION COMPLETE");
        _output.WriteLine("---------------------------------------------------------------------------------");
        _output.WriteLine("");
    }

    private async Task ValidateFhirQueryConfiguration(DataAcquisitionDbContext db, string facilityId)
    {
        _output.WriteLine("");
        _output.WriteLine("  --- FhirQueryConfiguration ---");

        var config = await db.FhirQueryConfigurations
            .FirstOrDefaultAsync(c => c.FacilityId == facilityId);

        Assert.NotNull(config);
        Assert.False(string.IsNullOrWhiteSpace(config.FhirServerBaseUrl), "FhirServerBaseUrl should be set");
        Assert.True(config.MaxConcurrentRequests > 0, "MaxConcurrentRequests should be > 0");
        Assert.True(config.MaxRetries > 0, "MaxRetries should be > 0");

        _output.WriteLine($"      FhirServerBaseUrl     = {config.FhirServerBaseUrl}");
        _output.WriteLine($"      MaxConcurrentRequests = {config.MaxConcurrentRequests}");
        _output.WriteLine($"      MaxRetries            = {config.MaxRetries}");
        _output.WriteLine("  --- FhirQueryConfiguration PASSED ---");
    }

    private async Task ValidateQueryPlans(DataAcquisitionDbContext db, string facilityId, string expectedMeasureId)
    {
        _output.WriteLine("");
        _output.WriteLine("  --- QueryPlan ---");

        var queryPlans = await db.QueryPlans
            .Where(qp => qp.FacilityId == facilityId)
            .ToListAsync();

        Assert.True(queryPlans.Count >= 2,
            $"Expected at least 2 query plans (Discharge + Monthly) but found {queryPlans.Count}");

        var dischargePlan = queryPlans.FirstOrDefault(qp => qp.Type == Frequency.Discharge);
        Assert.NotNull(dischargePlan);
        Assert.Equal(expectedMeasureId, dischargePlan.PlanName);
        Assert.True(dischargePlan.InitialQueries?.Count > 0, "Discharge plan should have InitialQueries");
        Assert.True(dischargePlan.SupplementalQueries?.Count > 0, "Discharge plan should have SupplementalQueries");

        _output.WriteLine($"      Discharge Plan:");
        _output.WriteLine($"        PlanName            = {dischargePlan.PlanName}");
        _output.WriteLine($"        InitialQueries      = {dischargePlan.InitialQueries?.Count}");
        _output.WriteLine($"        SupplementalQueries = {dischargePlan.SupplementalQueries?.Count}");

        var monthlyPlan = queryPlans.FirstOrDefault(qp => qp.Type == Frequency.Monthly);
        Assert.NotNull(monthlyPlan);
        Assert.Equal(expectedMeasureId, monthlyPlan.PlanName);
        Assert.True(monthlyPlan.InitialQueries?.Count > 0, "Monthly plan should have InitialQueries");
        Assert.True(monthlyPlan.SupplementalQueries?.Count > 0, "Monthly plan should have SupplementalQueries");

        _output.WriteLine($"      Monthly Plan:");
        _output.WriteLine($"        PlanName            = {monthlyPlan.PlanName}");
        _output.WriteLine($"        InitialQueries      = {monthlyPlan.InitialQueries?.Count}");
        _output.WriteLine($"        SupplementalQueries = {monthlyPlan.SupplementalQueries?.Count}");
        _output.WriteLine("  --- QueryPlan PASSED ---");
    }

    private async Task ValidateDataAcquisitionLogs(
        DataAcquisitionDbContext db, string facilityId, string reportId, List<string> expectedPatientIds)
    {
        _output.WriteLine("");
        _output.WriteLine("  --- DataAcquisitionLog ---");

        var logs = await PipelineSnapshot.GetAcquisitionLogsAsync(db, facilityId, reportId);

        Assert.True(logs.Count > 0,
            $"Expected DataAcquisitionLog rows for ReportTrackingId={reportId} but found none");

        var patientsInLogs = logs
            .Where(l => l.PatientId != null)
            .Select(l => l.PatientId!)
            .Distinct()
            .OrderBy(p => p)
            .ToList();

        foreach (var patientId in expectedPatientIds)
        {
            Assert.Contains(patientId, patientsInLogs);
        }

        var failedLogs = logs.Where(l => l.Status == RequestStatus.Failed || l.Status == RequestStatus.MaxRetriesReached).ToList();
        Assert.True(failedLogs.Count == 0,
            $"Found {failedLogs.Count} failed log(s): " +
            string.Join(", ", failedLogs.Select(l => $"Id={l.Id} Patient={l.PatientId} Status={l.Status}")));

        var completedCount = logs.Count(l => l.Status == RequestStatus.Completed);
        _output.WriteLine($"      Total Logs   = {logs.Count}");
        _output.WriteLine($"      Completed    = {completedCount}");
        _output.WriteLine($"      Patients     = {expectedPatientIds.Count}");

        foreach (var patientId in expectedPatientIds)
        {
            var patientLogs = logs.Where(l => l.PatientId == patientId).ToList();
            var statusBreakdown = patientLogs
                .GroupBy(l => l.Status)
                .Select(g => $"{g.Key}={g.Count()}");
            _output.WriteLine($"      Patient {patientId,-12} {patientLogs.Count} log(s) [{string.Join(", ", statusBreakdown)}]");
        }

        _output.WriteLine("  --- DataAcquisitionLog PASSED ---");
    }

    private async Task ValidateFhirQueries(DataAcquisitionDbContext db, string facilityId, string reportId)
    {
        _output.WriteLine("");
        _output.WriteLine("  --- FhirQuery ---");

        var queries = await db.FhirQueries
            .Include(q => q.FhirQueryResourceTypes)
            .Where(q => q.FacilityId == facilityId &&
                        q.DataAcquisitionLog.ReportTrackingId == reportId)
            .ToListAsync();

        Assert.True(queries.Count > 0, "Expected FhirQuery rows for this report but found none");

        var byType = queries.GroupBy(q => q.QueryType)
            .Select(g => $"{g.Key}={g.Count()}");
        _output.WriteLine($"      Total Queries = {queries.Count}");
        _output.WriteLine($"      By Type       = {string.Join(", ", byType)}");
        _output.WriteLine("  --- FhirQuery PASSED ---");
    }

    private async Task ValidateReferenceResources(DataAcquisitionDbContext db, string facilityId)
    {
        _output.WriteLine("");
        _output.WriteLine("  --- ReferenceResources ---");

        var resources = await db.ReferenceResources
            .Where(r => r.FacilityId == facilityId)
            .GroupBy(r => new { r.ResourceType, r.QueryPhase })
            .Select(g => new { g.Key.ResourceType, g.Key.QueryPhase, Count = g.Count() })
            .OrderBy(x => x.ResourceType)
            .ToListAsync();

        Assert.True(resources.Count > 0, "Expected ReferenceResources rows for the facility but found none");

        var totalCount = resources.Sum(r => r.Count);
        _output.WriteLine($"      Total Resources   = {totalCount}");
        _output.WriteLine($"      Type/Phase Groups = {resources.Count}");
        _output.WriteLine("  --- ReferenceResources PASSED ---");
    }
}
