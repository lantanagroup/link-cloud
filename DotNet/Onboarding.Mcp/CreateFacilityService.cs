using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using ModelContextProtocol;

namespace LantanaGroup.Link.Onboarding.Mcp;

public sealed record CreatedFacility(string FacilityId, string FacilityName, string TimeZone, string? VendorVersionId);
public sealed record CreateFacilityResult(string Status, int? HttpStatus, CreatedFacility? Facility, string Message);

public sealed class CreateFacilityService(IFacilityServiceClient facilityClient)
{
    public async Task<CreateFacilityResult> CreateAsync(string facilityId, string facilityName,
        string timeZone, string vendorVersionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        facilityId = facilityId?.Trim() ?? "";
        facilityName = facilityName?.Trim() ?? "";
        timeZone = timeZone?.Trim() ?? "";
        vendorVersionId = vendorVersionId?.Trim() ?? "";
        if (!Regex.IsMatch(facilityId, "^[a-zA-Z0-9-]+$", RegexOptions.CultureInvariant))
            throw new McpException("facilityId is required and may contain only letters, numbers, and hyphens. Tenant also enforces its configured ID policy.");
        if (string.IsNullOrWhiteSpace(facilityName) || facilityName.Any(char.IsControl) || facilityName.IndexOfAny(['<', '>']) >= 0)
            throw new McpException("facilityName is required and must be plain text without control characters.");
        try
        {
            if (!TimeZoneInfo.FindSystemTimeZoneById(timeZone).HasIanaId)
                throw new McpException("timeZone must be an IANA ID, for example America/New_York.");
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            throw new McpException("timeZone must be a valid IANA ID, for example America/New_York.");
        }
        Guid? vendorId = null;
        if (vendorVersionId.Length > 0)
        {
            if (!Guid.TryParse(vendorVersionId, out var parsed) || parsed == Guid.Empty)
                throw new McpException("vendorVersionId must be an existing vendor-version UUID supplied by the user, or an empty string to omit it.");
            vendorId = parsed;
        }

        var createAttempted = false;
        try
        {
            var existing = await facilityClient.GetAsync(facilityId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (existing.IsSuccessStatusCode)
                return new("already_exists", existing.StatusCode, null,
                    "The facility already exists. No changes were made.");
            if (existing.StatusCode != (int)HttpStatusCode.NotFound)
                return new("unavailable", existing.StatusCode == 0 ? null : existing.StatusCode, null,
                    "Tenant could not confirm that the facility is absent. Creation was not attempted.");

            var request = new FacilityModel
            {
                FacilityId = facilityId, FacilityName = facilityName, TimeZone = timeZone, VendorVersionId = vendorId,
                ScheduledReports = new TenantScheduledReportConfig { Daily = [], Weekly = [], Monthly = [] }
            };
            createAttempted = true;
            var response = await facilityClient.CreateAsync(request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var status = response.StatusCode;
            if (response.StatusCode != (int)HttpStatusCode.Created)
            {
                var uncertain = status >= 500 || status < 400 || response.StatusCode == (int)HttpStatusCode.RequestTimeout;
                return new(uncertain ? "unknown" : "rejected", status == 0 ? null : status, null, uncertain
                    ? "Tenant did not confirm creation. The facility may have been saved. Check Tenant before retrying."
                    : "Tenant rejected creation. Check its facility ID policy, vendor version, permissions, and required fields. No update was attempted.");
            }
            var facility = response.Body ?? throw new JsonException("Missing facility response.");
            if (facility.FacilityId != facilityId || facility.FacilityName is null || string.IsNullOrEmpty(facility.TimeZone))
                throw new JsonException("Unexpected facility response.");
            var created = new CreatedFacility(facilityId, facility.FacilityName, facility.TimeZone,
                facility.VendorVersionId?.ToString());
            return new("created", status, created,
                "Tenant confirmed facility creation. Connection/query-plan configuration and reporting enrollment require separate steps.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure(createAttempted, "Tenant request timed out.");
        }
        catch (HttpRequestException)
        {
            return Failure(createAttempted, "Cannot reach Tenant.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return Failure(createAttempted, "Tenant returned an unexpected response.");
        }
    }

    private static CreateFacilityResult Failure(bool createAttempted, string reason) =>
        new(createAttempted ? "unknown" : "unavailable", null, null, reason + (createAttempted
            ? " Creation may have succeeded. Check Tenant before retrying."
            : " Creation was not attempted."));
}
