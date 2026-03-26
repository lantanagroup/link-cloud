using LantanaGroup.Link.Tenant.Repository.Context;
using LantanaGroup.Link.Tests.E2ETests.Helpers;
using Xunit;

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
        output.WriteLine("");
        output.WriteLine("=================================================================================");
        output.WriteLine("  TENANT DATABASE VALIDATION");
        output.WriteLine($"  FacilityId: {facilityId}");
        output.WriteLine("=================================================================================");

        await using var db = DatabaseConnectionFactory.CreateTenantDbContext();

        await ValidateFacilityExists(db, facilityId);
        await ValidateFacilityProperties(db, facilityId);
        await ValidateScheduledReports(db, facilityId, expectedMeasureId);

        output.WriteLine("---------------------------------------------------------------------------------");
        output.WriteLine("  TENANT DATABASE VALIDATION COMPLETE");
        output.WriteLine("---------------------------------------------------------------------------------");
        output.WriteLine("");
    }

    private async Task ValidateFacilityExists(TenantDbContext db, string facilityId)
    {
        output.WriteLine("");
        output.WriteLine("  --- Facility Exists ---");

        var facility = await PipelineSnapshot.GetFacilityAsync(db, facilityId);

        Assert.NotNull(facility);
        Assert.Equal(facilityId, facility.FacilityId);

        output.WriteLine($"      Id         = {facility.Id}");
        output.WriteLine($"      FacilityId = {facility.FacilityId}");
        output.WriteLine("  --- Facility Exists PASSED ---");
    }

    private async Task ValidateFacilityProperties(TenantDbContext db, string facilityId)
    {
        output.WriteLine("");
        output.WriteLine("  --- FacilityProperties ---");

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

        output.WriteLine($"      FacilityName = {facility.FacilityName}");
        output.WriteLine($"      TimeZone     = {facility.TimeZone}");
        output.WriteLine($"      IsDeleted    = {facility.IsDeleted}");
        output.WriteLine($"      CreateDate   = {facility.CreateDate:O}");
        output.WriteLine("  --- FacilityProperties PASSED ---");
    }

    private async Task ValidateScheduledReports(TenantDbContext db, string facilityId, string expectedMeasureId)
    {
        output.WriteLine("");
        output.WriteLine("  --- ScheduledReports ---");

        var facility = await PipelineSnapshot.GetFacilityAsync(db, facilityId);
        Assert.NotNull(facility);

        Assert.NotNull(facility.ScheduledReports);

        // The smoke test creates a facility with the measure in the Monthly array
        var monthly = facility.ScheduledReports.Monthly ?? [];
        Assert.True(monthly.Length > 0,
            "Expected at least one Monthly scheduled report measure but found none");
        Assert.Contains(expectedMeasureId, monthly);

        var daily = facility.ScheduledReports.Daily ?? [];
        var weekly = facility.ScheduledReports.Weekly ?? [];

        output.WriteLine($"      Monthly = [{string.Join(", ", monthly)}]");
        output.WriteLine($"      Daily   = [{string.Join(", ", daily)}]");
        output.WriteLine($"      Weekly  = [{string.Join(", ", weekly)}]");
        output.WriteLine("  --- ScheduledReports PASSED ---");
    }
}

