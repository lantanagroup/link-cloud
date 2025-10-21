using LantanaGroup.Link.Automation.Link.Helpers;

namespace LantanaGroup.Link.Automation.Link.Validation;

public class TenantDatabaseValidator
{
    private const int MaxErrors = 100;
    private readonly IAutomationOutput _output;
    private readonly PipelineDataReader _reader;

    public TenantDatabaseValidator(IAutomationOutput output, PipelineDataReader reader)
    {
        _output = output;
        _reader = reader;
    }

    public async Task ValidateAllAsync(string facilityId, string expectedMeasureId)
    {
        var errors = new List<string>();

        try
        {
            await ValidateFacilityExists(facilityId, errors);
            await ValidateFacilityProperties(facilityId, errors);
            await ValidateScheduledReports(facilityId, expectedMeasureId, errors);
        }
        catch (Exception ex)
        {
            AddError(errors, $"Unhandled exception during tenant DB validation: {ex.Message}");
        }

        if (errors.Count == 0)
        {
            _output.WriteLine("TENANT DATABASE VALIDATION: Passed");
            return;
        }

        _output.WriteLine($"TENANT DATABASE VALIDATION: Failed ({errors.Count} issue(s))");
        foreach (var error in errors)
        {
            _output.WriteLine($"  - {error}");
        }

        throw new InvalidOperationException($"TENANT DATABASE VALIDATION failed with {errors.Count} issue(s).");
    }

    private static void AddError(List<string> errors, string message)
    {
        if (errors.Count < MaxErrors)
            errors.Add(message);
    }

    private async Task ValidateFacilityExists(string facilityId, List<string> errors)
    {
        var facility = await _reader.GetFacilityAsync(facilityId);
        if (facility == null)
        {
            AddError(errors, $"Facility {facilityId} not found.");
            return;
        }

        if (facility.FacilityId != facilityId)
            AddError(errors, $"FacilityId mismatch: expected {facilityId}, actual {facility.FacilityId}");
    }

    private async Task ValidateFacilityProperties(string facilityId, List<string> errors)
    {
        var facility = await _reader.GetFacilityAsync(facilityId);
        if (facility == null)
            return;

        if (string.IsNullOrWhiteSpace(facility.FacilityName)) AddError(errors, "FacilityName should be populated.");
        if (string.IsNullOrWhiteSpace(facility.TimeZone)) AddError(errors, "TimeZone should be populated.");
        if (facility.IsDeleted) AddError(errors, "Facility should not be soft-deleted.");
        if (facility.CreateDate <= DateTime.MinValue) AddError(errors, "CreateDate should be populated.");
    }

    private async Task ValidateScheduledReports(string facilityId, string expectedMeasureId, List<string> errors)
    {
        var facility = await _reader.GetFacilityAsync(facilityId);
        if (facility == null)
            return;

        if (facility.ScheduledReports == null)
        {
            AddError(errors, "ScheduledReports should be populated.");
            return;
        }

        var monthly = facility.ScheduledReports.Monthly ?? [];
        if (monthly.Length == 0)
            AddError(errors, "Expected at least one Monthly scheduled report measure.");
        else if (!monthly.Contains(expectedMeasureId))
            AddError(errors, $"Monthly scheduled reports do not contain expected measure {expectedMeasureId}.");
    }
}
