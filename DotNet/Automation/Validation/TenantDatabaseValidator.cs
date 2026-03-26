using LantanaGroup.Link.Automation.Helpers;
using LantanaGroup.Link.Tenant.Repository.Context;
using Xunit;
using Xunit.Abstractions;

namespace LantanaGroup.Link.Automation.Validation;

/// <summary>
/// Validates the Tenant service's database state after a smoke test run.
/// </summary>
public class TenantDatabaseValidator
{
    private readonly ITestOutputHelper _output;
    private readonly DatabaseConnectionFactory _dbFactory;

    public TenantDatabaseValidator(ITestOutputHelper output, DatabaseConnectionFactory dbFactory)
    {
        _output = output;
        _dbFactory = dbFactory;
    }

    public async Task ValidateAllAsync(string facilityId, string expectedMeasureId)
    {
        _output.WriteLine("");
        _output.WriteLine("=================================================================================");
        _output.WriteLine("  TENANT DATABASE VALIDATION");
        _output.WriteLine($"  FacilityId: {facilityId}");
        _output.WriteLine("=================================================================================");

        await using var db = _dbFactory.CreateTenantDbContext();

        await ValidateFacilityExists(db, facilityId);
        await ValidateFacilityProperties(db, facilityId);
        await ValidateScheduledReports(db, facilityId, expectedMeasureId);

        _output.WriteLine("---------------------------------------------------------------------------------");
        _output.WriteLine("  TENANT DATABASE VALIDATION COMPLETE");
        _output.WriteLine("---------------------------------------------------------------------------------");
        _output.WriteLine("");
    }

    private async Task ValidateFacilityExists(TenantDbContext db, string facilityId)
    {
        _output.WriteLine("");
        _output.WriteLine("  --- Facility Exists ---");

        var facility = await PipelineSnapshot.GetFacilityAsync(db, facilityId);

        Assert.NotNull(facility);
        Assert.Equal(facilityId, facility.FacilityId);

        _output.WriteLine($"      Id         = {facility.Id}");
        _output.WriteLine($"      FacilityId = {facility.FacilityId}");
        _output.WriteLine("  --- Facility Exists PASSED ---");
    }

    private async Task ValidateFacilityProperties(TenantDbContext db, string facilityId)
    {
        _output.WriteLine("");
        _output.WriteLine("  --- FacilityProperties ---");

        var facility = await PipelineSnapshot.GetFacilityAsync(db, facilityId);
        Assert.NotNull(facility);

        Assert.False(string.IsNullOrWhiteSpace(facility.FacilityName), "FacilityName should be set");
        Assert.False(string.IsNullOrWhiteSpace(facility.TimeZone), "TimeZone should be set");
        Assert.False(facility.IsDeleted, "Facility should not be soft-deleted");
        Assert.True(facility.CreateDate > DateTime.MinValue, "CreateDate should be populated");

        _output.WriteLine($"      FacilityName = {facility.FacilityName}");
        _output.WriteLine($"      TimeZone     = {facility.TimeZone}");
        _output.WriteLine($"      IsDeleted    = {facility.IsDeleted}");
        _output.WriteLine($"      CreateDate   = {facility.CreateDate:O}");
        _output.WriteLine("  --- FacilityProperties PASSED ---");
    }

    private async Task ValidateScheduledReports(TenantDbContext db, string facilityId, string expectedMeasureId)
    {
        _output.WriteLine("");
        _output.WriteLine("  --- ScheduledReports ---");

        var facility = await PipelineSnapshot.GetFacilityAsync(db, facilityId);
        Assert.NotNull(facility);

        Assert.NotNull(facility.ScheduledReports);

        var monthly = facility.ScheduledReports.Monthly ?? [];
        Assert.True(monthly.Length > 0,
            "Expected at least one Monthly scheduled report measure but found none");
        Assert.Contains(expectedMeasureId, monthly);

        var daily = facility.ScheduledReports.Daily ?? [];
        var weekly = facility.ScheduledReports.Weekly ?? [];

        _output.WriteLine($"      Monthly = [{string.Join(", ", monthly)}]");
        _output.WriteLine($"      Daily   = [{string.Join(", ", daily)}]");
        _output.WriteLine($"      Weekly  = [{string.Join(", ", weekly)}]");
        _output.WriteLine("  --- ScheduledReports PASSED ---");
    }
}
