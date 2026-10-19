using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using ModelContextProtocol;

namespace LantanaGroup.Link.Onboarding.Mcp;

public sealed record ConnectionValidationResult(string Status, bool? IsConnected, int? HttpStatus, string Message);
public sealed record DataAcquisitionConfiguration(string FacilityId, string FhirServerBaseUrl,
    int? MaxConcurrentRequests, int? MaxRetries, string? MinAcquisitionPullTime, string? MaxAcquisitionPullTime);
public sealed record GetDataAcquisitionConfigurationResult(string Status, int? HttpStatus, string FacilityId,
    string Message, DataAcquisitionConfiguration? Configuration = null);
public sealed record SaveDataAcquisitionResult(string Status, int? HttpStatus, string FacilityId, string Message,
    ConnectionValidationResult? ConnectionValidation = null);

public sealed class DataAcquisitionConfigurationService(HttpClient httpClient, IOptions<OnboardingOptions> options)
{
    public async Task<GetDataAcquisitionConfigurationResult> GetAsync(string facilityId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        facilityId = facilityId?.Trim() ?? "";
        if (!Regex.IsMatch(facilityId, "^[a-zA-Z0-9-]+$", RegexOptions.CultureInvariant))
            throw new McpException("facilityId must contain only letters, digits, and hyphens.");
        try
        {
            using var request = Request(HttpMethod.Get, options.Value.DataAcquisitionApiBaseUrl,
                Uri.EscapeDataString(facilityId) + "/fhirQueryConfiguration", options.Value.DataAcquisitionBearerToken);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var status = (int)response.StatusCode;
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                var problem = await ReadObject(response, cancellationToken);
                if (problem["status"]?.GetValue<int>() == 404 &&
                    problem["detail"]?.GetValue<string>() == $"No FhirQueryConfiguration found for facilityId: {facilityId}")
                    return new("not_found", status, facilityId, "No data acquisition configuration exists for this facility.");
            }
            if (response.StatusCode != HttpStatusCode.OK)
                return new("unavailable", status, facilityId, "Could not retrieve configuration. Check permissions and the DataAcquisition API route.");
            var body = await ReadObject(response, cancellationToken);
            if (body["facilityId"]?.GetValue<string>() != facilityId)
                throw new JsonException();
            var baseUrl = body["fhirServerBaseUrl"]?.GetValue<string>();
            // Legacy settings may contain URL credentials or query secrets. Never return them to the model.
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
                || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new JsonException();
            var configuration = new DataAcquisitionConfiguration(facilityId, baseUrl!,
                body["maxConcurrentRequests"]?.GetValue<int>(), body["maxRetries"]?.GetValue<int>(),
                body["minAcquisitionPullTime"]?.GetValue<string>(), body["maxAcquisitionPullTime"]?.GetValue<string>());
            return new("available", status, facilityId, "Data acquisition configuration retrieved. Authentication details are excluded.", configuration);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return new("unavailable", null, facilityId, "Configuration retrieval timed out."); }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or FormatException)
        { return new("unavailable", null, facilityId, "Could not safely retrieve the data acquisition configuration."); }
    }

    public async Task<SaveDataAcquisitionResult> SaveAsync(string facilityId, string fhirServerBaseUrl,
        int maxConcurrentRequests, int maxRetries, string minAcquisitionPullTime, string maxAcquisitionPullTime,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        facilityId = facilityId?.Trim() ?? "";
        fhirServerBaseUrl = fhirServerBaseUrl?.Trim() ?? "";
        if (!Regex.IsMatch(facilityId, "^[a-zA-Z0-9-]+$", RegexOptions.CultureInvariant))
            throw new McpException("facilityId must contain only letters, digits, and hyphens.");
        if (fhirServerBaseUrl.Length > 0 && (!Uri.TryCreate(fhirServerBaseUrl, UriKind.Absolute, out var url)
            || url.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(url.UserInfo)
            || !string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.Fragment)))
            throw new McpException("Use an absolute HTTP(S) FHIR base URL without credentials, query, or fragment.");
        if (maxConcurrentRequests != -1 && maxConcurrentRequests < 1)
            throw new McpException("maxConcurrentRequests must be positive, or -1 to preserve/default.");
        if (maxRetries < -1 || maxRetries > 10)
            throw new McpException("maxRetries must be 0 through 10, or -1 to preserve/default.");
        ValidateTime(minAcquisitionPullTime);
        ValidateTime(maxAcquisitionPullTime);
        var attempted = false;
        try
        {
            using var tenantRequest = Request(HttpMethod.Get, options.Value.TenantApiBaseUrl,
                "Facility/" + Uri.EscapeDataString(facilityId), options.Value.BearerToken);
            using var tenantResponse = await httpClient.SendAsync(tenantRequest, cancellationToken);
            if (tenantResponse.StatusCode != HttpStatusCode.OK)
                return new("unavailable", (int)tenantResponse.StatusCode, facilityId, "Could not read the facility. No configuration write was attempted.");
            var tenant = await ReadObject(tenantResponse, cancellationToken);
            if (tenant["facilityId"]?.GetValue<string>() != facilityId)
                throw new JsonException();
            var timeZone = tenant["timeZone"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(timeZone)) throw new JsonException();
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZone);

            using var lookup = Request(HttpMethod.Get, options.Value.DataAcquisitionApiBaseUrl,
                Uri.EscapeDataString(facilityId) + "/fhirQueryConfiguration", options.Value.DataAcquisitionBearerToken);
            using var existingResponse = await httpClient.SendAsync(lookup, cancellationToken);
            var exists = existingResponse.StatusCode == HttpStatusCode.OK;
            if (!exists && existingResponse.StatusCode != HttpStatusCode.NotFound)
                return new("unavailable", (int)existingResponse.StatusCode, facilityId, "Could not read the existing configuration. No write was attempted.");
            if (!exists)
            {
                // An unmatched route also returns 404. Only the service's explicit missing-record
                // response authorizes creation; never turn an ambiguous 404 into a POST.
                var problem = await ReadObject(existingResponse, cancellationToken);
                if (problem["status"]?.GetValue<int>() != 404 ||
                    problem["detail"]?.GetValue<string>() != $"No FhirQueryConfiguration found for facilityId: {facilityId}")
                    return new("unavailable", 404, facilityId,
                        "DataAcquisition did not confirm a missing configuration. Check its API base URL and route version. No write was attempted.");
            }
            JsonObject body = new();
            if (exists)
            {
                var existing = await ReadObject(existingResponse, cancellationToken);
                if (existing["facilityId"]?.GetValue<string>() != facilityId || string.IsNullOrWhiteSpace(existing["id"]?.GetValue<string>()))
                    throw new JsonException();
                // PUT replaces these fields. Retain authentication privately; never expose it to the model.
                foreach (var key in new[] { "id", "fhirServerBaseUrl", "authentication", "maxConcurrentRequests", "maxRetries", "minAcquisitionPullTime", "maxAcquisitionPullTime" })
                    body[key] = existing[key]?.DeepClone();
            }
            body["facilityId"] = facilityId;
            body["timeZone"] = timeZone;
            if (fhirServerBaseUrl.Length > 0) body["fhirServerBaseUrl"] = fhirServerBaseUrl;
            if (string.IsNullOrWhiteSpace(body["fhirServerBaseUrl"]?.GetValue<string>()))
                throw new McpException("fhirServerBaseUrl is required when adding a configuration.");
            if (maxConcurrentRequests != -1) body["maxConcurrentRequests"] = maxConcurrentRequests;
            else if (!exists) body["maxConcurrentRequests"] = 1;
            if (maxRetries != -1) body["maxRetries"] = maxRetries;
            ApplyTime(body, "minAcquisitionPullTime", minAcquisitionPullTime);
            ApplyTime(body, "maxAcquisitionPullTime", maxAcquisitionPullTime);
            using var request = Request(exists ? HttpMethod.Put : HttpMethod.Post,
                options.Value.DataAcquisitionApiBaseUrl, "fhirQueryConfiguration", options.Value.DataAcquisitionBearerToken);
            request.Content = JsonContent.Create(body);
            attempted = true;
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var status = (int)response.StatusCode;
            if (response.StatusCode == (exists ? HttpStatusCode.Accepted : HttpStatusCode.Created))
            {
                var validation = fhirServerBaseUrl.Length > 0
                    ? await ValidateConnectionAsync(fhirServerBaseUrl, cancellationToken)
                    : new ConnectionValidationResult("not_requested", null, null, "The FHIR base URL was not supplied; connection validation was not run.");
                return new(exists ? "updated" : "created", status, facilityId,
                    "Data acquisition configuration saved. Authentication was preserved. " + validation.Message, validation);
            }
            if (status == 404)
                return new("rejected", status, facilityId,
                    "The configuration write endpoint or target was not found. Check the DataAcquisition API base URL and route version; no retry was attempted.");
            var uncertain = status >= 500 || status < 400 || status == 408;
            return new(uncertain ? "unknown" : "rejected", status, facilityId, uncertain
                ? "The write outcome is uncertain. Check DataAcquisition before retrying."
                : "DataAcquisition rejected the configuration. Check permissions and settings; no retry was attempted.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return Failure(); }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or TimeZoneNotFoundException or InvalidTimeZoneException)
        { return Failure(); }

        SaveDataAcquisitionResult Failure() => new(attempted ? "unknown" : "unavailable", null, facilityId,
            attempted ? "The write outcome is uncertain. Check DataAcquisition before retrying." : "Could not safely read the existing settings. No write was attempted.");
    }

    public async Task<ConnectionValidationResult> ValidateConnectionAsync(string fhirServerBaseUrl, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        fhirServerBaseUrl = fhirServerBaseUrl?.Trim() ?? "";
        if (!Uri.TryCreate(fhirServerBaseUrl, UriKind.Absolute, out var url)
            || url.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(url.UserInfo)
            || !string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.Fragment))
            throw new McpException("Use an absolute HTTP(S) FHIR base URL without credentials, query, or fragment.");
        try
        {
            using var request = Request(HttpMethod.Get, options.Value.DataAcquisitionApiBaseUrl,
                "connectionValidation/$validate?fhirServerUrl=" + Uri.EscapeDataString(fhirServerBaseUrl),
                options.Value.DataAcquisitionBearerToken);
            using var response = await httpClient.SendAsync(request, token);
            var status = (int)response.StatusCode;
            if (response.StatusCode != HttpStatusCode.OK)
                return new(status == 502 ? "failed" : "unavailable", status == 502 ? false : null, status,
                    status == 502 ? "The FHIR connection check failed."
                        : "Connection validation was unavailable. Check the DataAcquisition validation endpoint.");
            var result = await ReadObject(response, token);
            var connected = result["isConnected"]?.GetValue<bool>() ?? throw new JsonException();
            // Do not expose raw upstream errors, which can contain server internals or response bodies.
            return new(connected ? "connected" : "failed", connected, status, connected
                ? "The FHIR server connection check succeeded (unauthenticated metadata/CapabilityStatement check)."
                : "The FHIR server did not pass the metadata/CapabilityStatement check.");
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        { return new("unavailable", null, null, "Connection validation timed out."); }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException)
        { return new("unavailable", null, null, "Connection validation could not be completed."); }
    }

    private static void ValidateTime(string value)
    {
        if (value is "" or "clear") return;
        if (!TimeSpan.TryParseExact(value, "hh\\:mm\\:ss", CultureInfo.InvariantCulture, out _))
            throw new McpException("Pull times must be HH:mm:ss, empty to preserve/default, or clear to remove the limit.");
    }
    private static void ApplyTime(JsonObject body, string key, string value)
    {
        if (value == "clear") body[key] = null;
        else if (!string.IsNullOrEmpty(value)) body[key] = value;
    }
    private static async Task<JsonObject> ReadObject(HttpResponseMessage response, CancellationToken token) =>
        await JsonNode.ParseAsync(await response.Content.ReadAsStreamAsync(token), cancellationToken: token) as JsonObject ?? throw new JsonException();
    private static HttpRequestMessage Request(HttpMethod method, string baseUrl, string path, string? token)
    {
        var request = new HttpRequestMessage(method, new Uri(new Uri(baseUrl), path));
        if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
