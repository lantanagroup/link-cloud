using LantanaGroup.Link.Tenant.Repository.Context;
using LantanaGroup.Link.Tests.E2ETests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace LantanaGroup.Link.Tests.E2ETests.Validation;

/// <summary>
/// Validates the Tenant service's database state after a smoke test run.
/// Ensures the facility record exists with the expected configuration,
/// including time zone, scheduled report measures, and soft-delete flag.
/// </summary>
public class TenantDatabaseValidator(DualOutputHelper output)
{
    public async Task ValidateAllAsync(string facilityId, string expectedMeasureId)
    {
        output.WriteLine("\n");
        output.WriteLine($"\n=== Tenant Database Validation: FacilityId={facilityId} ===\n");

        await using var db = DatabaseConnectionFactory.CreateTenantDbContext();

        await ValidateFacilityExists(db, facilityId);
        await ValidateFacilityProperties(db, facilityId);
        await ValidateScheduledReports(db, facilityId, expectedMeasureId);

        output.WriteLine("\n=== Tenant Database Validation Complete ===\n");
    }

    private async Task ValidateFacilityExists(TenantDbContext db, string facilityId)
    {
        output.WriteLine("[Facility] Validating existence...");

        var facility = await PipelineSnapshot.GetFacilityAsync(db, facilityId);

        Assert.NotNull(facility);
        Assert.Equal(facilityId, facility.FacilityId);

        output.WriteLine($"  Found Facility: Id={facility.Id}, FacilityId={facility.FacilityId}");
        output.WriteLine("[Facility] PASS");
    }

    private async Task ValidateFacilityProperties(TenantDbContext db, string facilityId)
    {
        output.WriteLine("[FacilityProperties] Validating...");

        var facility = await PipelineSnapshot.GetFacilityAsync(db, facilityId);
        Assert.NotNull(facility);

        Assert.False(string.IsNullOrWhiteSpace(facility.FacilityName),
            "FacilityName should be set");

        Assert.False(string.IsNullOrWhiteSpace(facility.TimeZone),
            "TimeZone should be set");

        Assert.False(facility.IsDeleted,
            "Facility should not be soft-deleted");

        Assert.True(facility.CreateDate > DateTime.MinValue,
            "CreateDate should be populated");

        output.WriteLine($"  FacilityName={facility.FacilityName}, " +
                         $"TimeZone={facility.TimeZone}, " +
                         $"IsDeleted={facility.IsDeleted}, " +
                         $"CreateDate={facility.CreateDate:O}");
        output.WriteLine("[FacilityProperties] PASS");
    }

    private async Task ValidateScheduledReports(TenantDbContext db, string facilityId, string expectedMeasureId)
    {
        output.WriteLine("[ScheduledReports] Validating...");

        var facility = await PipelineSnapshot.GetFacilityAsync(db, facilityId);
        Assert.NotNull(facility);

        Assert.NotNull(facility.ScheduledReports);

        // The smoke test creates a facility with the measure in the Monthly array
        var monthly = facility.ScheduledReports.Monthly ?? [];
        Assert.True(monthly.Length > 0,
            "Expected at least one Monthly scheduled report measure but found none");
        Assert.Contains(expectedMeasureId, monthly);

        output.WriteLine($"  Monthly=[{string.Join(", ", monthly)}]");

        var daily = facility.ScheduledReports.Daily ?? [];
        var weekly = facility.ScheduledReports.Weekly ?? [];

        output.WriteLine($"  Daily=[{string.Join(", ", daily)}], " +
                         $"Weekly=[{string.Join(", ", weekly)}]");
        output.WriteLine("[ScheduledReports] PASS");
    }
}

