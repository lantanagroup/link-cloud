using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Tests.E2ETests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LantanaGroup.Link.Tests.E2ETests.Validation;

/// <summary>
/// Validates the DataAcquisition service's database state after a smoke test run.
/// Delegates shared queries to <see cref="PipelineSnapshot"/> and applies strict assertions.
/// </summary>
public class DataAcquisitionDatabaseValidator(DualOutputHelper output)
{
    public async Task ValidateAllAsync(
        string facilityId,
        string reportId,
        string expectedMeasureId,
        List<string> expectedPatientIds)
    {
        output.WriteLine("\n");
        output.WriteLine($"\n=== DataAcquisition Database Validation: FacilityId={facilityId}, ReportId={reportId} ===\n");

        await using var db = DatabaseConnectionFactory.CreateDataAcquisitionDbContext();

        await ValidateFhirQueryConfiguration(db, facilityId);
        await ValidateQueryPlans(db, facilityId, expectedMeasureId);
        await ValidateDataAcquisitionLogs(db, facilityId, reportId, expectedPatientIds);
        await ValidateFhirQueries(db, facilityId, reportId);
        await ValidateReferenceResources(db, facilityId);

        output.WriteLine("\n=== DataAcquisition Database Validation Complete ===\n");
    }

    private async Task ValidateFhirQueryConfiguration(DataAcquisitionDbContext db, string facilityId)
    {
        output.WriteLine("[FhirQueryConfiguration] Validating...");

        var config = await db.FhirQueryConfigurations
            .FirstOrDefaultAsync(c => c.FacilityId == facilityId);

        Assert.NotNull(config);
        Assert.False(string.IsNullOrWhiteSpace(config.FhirServerBaseUrl), "FhirServerBaseUrl should be set");
        Assert.True(config.MaxConcurrentRequests > 0, "MaxConcurrentRequests should be > 0");
        Assert.True(config.MaxRetries > 0, "MaxRetries should be > 0");

        output.WriteLine($"  FhirServerBaseUrl={config.FhirServerBaseUrl}, " +
                         $"MaxConcurrentRequests={config.MaxConcurrentRequests}, MaxRetries={config.MaxRetries}");
        output.WriteLine("[FhirQueryConfiguration] PASS");
    }

    private async Task ValidateQueryPlans(DataAcquisitionDbContext db, string facilityId, string expectedMeasureId)
    {
        output.WriteLine("[QueryPlan] Validating...");

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

        output.WriteLine($"  Discharge: PlanName={dischargePlan.PlanName}, " +
                         $"InitialQueries={dischargePlan.InitialQueries?.Count}, " +
                         $"SupplementalQueries={dischargePlan.SupplementalQueries?.Count}");

        var monthlyPlan = queryPlans.FirstOrDefault(qp => qp.Type == Frequency.Monthly);
        Assert.NotNull(monthlyPlan);
        Assert.Equal(expectedMeasureId, monthlyPlan.PlanName);
        Assert.True(monthlyPlan.InitialQueries?.Count > 0, "Monthly plan should have InitialQueries");
        Assert.True(monthlyPlan.SupplementalQueries?.Count > 0, "Monthly plan should have SupplementalQueries");

        output.WriteLine($"  Monthly: PlanName={monthlyPlan.PlanName}, " +
                         $"InitialQueries={monthlyPlan.InitialQueries?.Count}, " +
                         $"SupplementalQueries={monthlyPlan.SupplementalQueries?.Count}");
        output.WriteLine("[QueryPlan] PASS");
    }

    private async Task ValidateDataAcquisitionLogs(
        DataAcquisitionDbContext db, string facilityId, string reportId, List<string> expectedPatientIds)
    {
        output.WriteLine("[DataAcquisitionLog] Validating...");

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
        output.WriteLine($"  {logs.Count} log(s), {completedCount} completed, " +
                         $"{expectedPatientIds.Count} patients found");

        foreach (var patientId in expectedPatientIds)
        {
            var patientLogs = logs.Where(l => l.PatientId == patientId).ToList();
            var statusBreakdown = patientLogs
                .GroupBy(l => l.Status)
                .Select(g => $"{g.Key}={g.Count()}");
            output.WriteLine($"  Patient {patientId}: {patientLogs.Count} log(s) [{string.Join(", ", statusBreakdown)}]");
        }

        output.WriteLine("[DataAcquisitionLog] PASS");
    }

    private async Task ValidateFhirQueries(DataAcquisitionDbContext db, string facilityId, string reportId)
    {
        output.WriteLine("[FhirQuery] Validating...");

        var queries = await db.FhirQueries
            .Include(q => q.FhirQueryResourceTypes)
            .Where(q => q.FacilityId == facilityId &&
                        q.DataAcquisitionLog.ReportTrackingId == reportId)
            .ToListAsync();

        Assert.True(queries.Count > 0, "Expected FhirQuery rows for this report but found none");

        var byType = queries.GroupBy(q => q.QueryType)
            .Select(g => $"{g.Key}={g.Count()}");
        output.WriteLine($"  {queries.Count} queries | {string.Join(", ", byType)}");
        output.WriteLine("[FhirQuery] PASS");
    }

    private async Task ValidateReferenceResources(DataAcquisitionDbContext db, string facilityId)
    {
        output.WriteLine("[ReferenceResources] Validating...");

        var resources = await db.ReferenceResources
            .Where(r => r.FacilityId == facilityId)
            .GroupBy(r => new { r.ResourceType, r.QueryPhase })
            .Select(g => new { g.Key.ResourceType, g.Key.QueryPhase, Count = g.Count() })
            .OrderBy(x => x.ResourceType)
            .ToListAsync();

        Assert.True(resources.Count > 0, "Expected ReferenceResources rows for the facility but found none");

        var totalCount = resources.Sum(r => r.Count);
        output.WriteLine($"  {totalCount} resource(s) across {resources.Count} type/phase groups");
        output.WriteLine("[ReferenceResources] PASS");
    }
}

