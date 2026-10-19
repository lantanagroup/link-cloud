using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using ModelContextProtocol;

namespace LantanaGroup.Link.Onboarding.Mcp;

public sealed record CreatedFacility(string FacilityId, string FacilityName, string TimeZone, string? VendorVersionId);
public sealed record CreateFacilityResult(string Status, int? HttpStatus, CreatedFacility? Facility, string Message);

public sealed class CreateFacilityService(HttpClient httpClient, IOptions<OnboardingOptions> options)
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
                throw new McpException("vendorVersionId must be an existing vendor-version UUID from get_onboarding_options, or an empty string to omit it.");
            vendorId = parsed;
        }

        var createAttempted = false;
        try
        {
            using var lookup = Request(HttpMethod.Get, $"Facility/{Uri.EscapeDataString(facilityId)}");
            using var existing = await httpClient.SendAsync(lookup, cancellationToken);
            if (existing.IsSuccessStatusCode)
                return new("already_exists", (int)existing.StatusCode, null,
                    "The facility already exists. No changes were made.");
            if (existing.StatusCode != HttpStatusCode.NotFound)
                return new("unavailable", (int)existing.StatusCode, null,
                    "Tenant could not confirm that the facility is absent. Creation was not attempted.");

            using var request = Request(HttpMethod.Post, "Facility");
            request.Content = JsonContent.Create(new
            {
                facilityId, facilityName, timeZone, vendorVersionId = vendorId,
                scheduledReports = new { daily = Array.Empty<string>(), weekly = Array.Empty<string>(), monthly = Array.Empty<string>() }
            });
            createAttempted = true;
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var status = (int)response.StatusCode;
            if (response.StatusCode != HttpStatusCode.Created)
            {
                var uncertain = status >= 500 || status < 400 || response.StatusCode == HttpStatusCode.RequestTimeout;
                return new(uncertain ? "unknown" : "rejected", status, null, uncertain
                    ? "Tenant did not confirm creation. The facility may have been saved. Check Tenant before retrying."
                    : "Tenant rejected creation. Check its facility ID policy, vendor version, permissions, and required fields. No update was attempted.");
            }
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var facility = document.RootElement;
            if (facility.GetProperty("facilityId").GetString() != facilityId)
                throw new JsonException("Unexpected facility response.");
            var created = new CreatedFacility(facilityId,
                facility.GetProperty("facilityName").GetString() ?? throw new JsonException(),
                facility.GetProperty("timeZone").GetString() ?? throw new JsonException(),
                facility.TryGetProperty("vendorVersionId", out var version) && version.ValueKind == JsonValueKind.String
                    ? version.GetString() : null);
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

    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, new Uri(new Uri(options.Value.TenantApiBaseUrl), path));
        if (!string.IsNullOrWhiteSpace(options.Value.BearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.BearerToken);
        return request;
    }

    private static CreateFacilityResult Failure(bool createAttempted, string reason) =>
        new(createAttempted ? "unknown" : "unavailable", null, null, reason + (createAttempted
            ? " Creation may have succeeded. Check Tenant before retrying."
            : " Creation was not attempted."));
}
