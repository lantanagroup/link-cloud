using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Automation.UI.Models.ApiHealth;
using Automation.UI.Services.ApiHealth.Seeding;
using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using StepNames = Automation.UI.Services.ApiHealth.TestSuites.ApiEndPointLibrary.AdminBffSteps;

namespace Automation.UI.Services.ApiHealth.TestSuites;

/// <summary>
/// Exercises the Admin BFF's unique aggregation endpoints via HTTP.
/// Tests both success (2xx) and error (4xx) paths for each endpoint.
/// Creates prerequisite data (facility) to exercise the full soft-delete/restore lifecycle.
/// </summary>
public sealed class AdminBffTestSuite : ServiceTestSuiteBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<AutomationConfig> _automationConfig;
    private readonly ICreateSystemToken _tokenService;
    private readonly IOptions<LinkTokenServiceSettings> _tokenSettings;
    private readonly IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> _bearerOptions;
    private readonly IApiHealthSeedContextAccessor _seedContext;
    private readonly ILogger<AdminBffTestSuite> _logger;

    public override string ServiceName => "AdminBff";

    public AdminBffTestSuite(
        IHttpClientFactory httpClientFactory,
        IOptions<AutomationConfig> automationConfig,
        ICreateSystemToken tokenService,
        IOptions<LinkTokenServiceSettings> tokenSettings,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> bearerOptions,
        IApiHealthSeedContextAccessor seedContext,
        ILogger<AdminBffTestSuite> logger)
    {
        _httpClientFactory = httpClientFactory;
        _automationConfig = automationConfig;
        _tokenService = tokenService;
        _tokenSettings = tokenSettings;
        _bearerOptions = bearerOptions;
        _seedContext = seedContext;
        _logger = logger;
    }

    public override IReadOnlyList<ApiEndpointDefinition> GetEndpointDefinitions() =>
        ApiEndPointLibrary.GetServiceEndpoints(ServiceName);

    public override IReadOnlyList<ApiHealthSeedRequirement> GetSeedRequirements() =>
    [
        ApiHealthSeedRequirement.ReportSchedule
    ];

    public override async Task<IReadOnlyList<ApiTestRunResult>> ExecuteAsync(CancellationToken ct = default)
    {
        var results = new List<ApiTestRunResult>();
        var baseUrl = _automationConfig.Value.AdminBffBase?.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            results.Add(new ApiTestRunResult
            {
                EndpointKey = $"{ServiceName}::{StepNames.HealthGet200}",
                ServiceName = ServiceName,
                Passed = false,
                ErrorMessage = "AdminBffBase URL is not configured in Automation settings.",
                RequestBody = "Request was not sent because the AdminBffBase URL is missing.",
                ResponseBody = "Response was not received because the AdminBffBase URL is missing."
            });
            return results;
        }

        // --- /api/monitor/health ---
        results.Add(await CallBffAsync(StepNames.HealthGet200, $"{baseUrl}/monitor/health", "GET", 200, ct));

        // --- /api/aggregate/facility/{id} lifecycle ---
        var facilityId = $"ApiHealth-BFF-{Guid.NewGuid():N}";
        var facilityCreated = false;

        try
        {
            var createResult = await CreateFacilityViaBffAsync(baseUrl, facilityId, ct);
            facilityCreated = createResult;

            if (facilityCreated)
            {
                results.Add(await CallBffAsync(StepNames.FacilityDelete200, $"{baseUrl}/aggregate/facility/{facilityId}", "DELETE", 200, ct));
                results.Add(await CallBffAsync(StepNames.FacilityRestorePatch200, $"{baseUrl}/aggregate/facility/{facilityId}/restore", "PATCH", 200, ct));
            }
            else
            {
                results.Add(MakeFailedResult(StepNames.FacilityDelete200, "Prerequisite: facility creation via BFF failed."));
                results.Add(MakeFailedResult(StepNames.FacilityRestorePatch200, "Prerequisite: facility creation via BFF failed."));
            }
        }
        finally
        {
            if (facilityCreated)
            {
                try { await CallBffRawAsync($"{baseUrl}/Facility/{facilityId}", "DELETE", ct); }
                catch { /* best effort */ }
            }
        }

        var fakeFacilityId = $"ApiHealth-BFF-{Guid.NewGuid():N}";
        results.Add(await CallBffAsync(StepNames.FacilityDelete404, $"{baseUrl}/aggregate/facility/{fakeFacilityId}", "DELETE", 404, ct));
        results.Add(await CallBffAsync(StepNames.FacilityRestorePatch404, $"{baseUrl}/aggregate/facility/{fakeFacilityId}/restore", "PATCH", 404, ct));

        // --- /api/aggregate/reports/summaries ---
        results.Add(await CallBffAsync(StepNames.SummariesGet200, $"{baseUrl}/aggregate/reports/summaries", "GET", 200, ct));

        // Seed-owned only: this suite requires ReportSchedule seed fixture.
        var reportScheduleId = _seedContext.Current?.Report?.ScheduleId;
        const string reportSeedMissing = "Report seed was unavailable. This lifecycle test requires seed-owned schedule data from the seeding phase.";

        // --- /api/aggregate/reports/summaries/{id} ---
        if (!string.IsNullOrWhiteSpace(reportScheduleId))
            results.Add(await CallBffAsync(StepNames.SummaryGet200, $"{baseUrl}/aggregate/reports/summaries/{reportScheduleId}", "GET", 200, ct));
        else
            results.Add(SkipStepAsync(StepNames.SummaryGet200, reportSeedMissing));

        var fakeReportId = Guid.NewGuid().ToString();
        results.Add(await CallBffAsync(StepNames.SummaryGet404, $"{baseUrl}/aggregate/reports/summaries/{fakeReportId}", "GET", 404, ct));

        // --- /api/aggregate/reports/{id} lifecycle ---

        if (!string.IsNullOrWhiteSpace(reportScheduleId))
        {
            results.Add(await CallBffAsync(StepNames.ReportDelete204, $"{baseUrl}/aggregate/reports/{reportScheduleId}", "DELETE", 204, ct));
            results.Add(await CallBffAsync(StepNames.ReportRestorePatch204, $"{baseUrl}/aggregate/reports/{reportScheduleId}/restore", "PATCH", 204, ct));
        }
        else
        {
            results.Add(MakeFailedResult(StepNames.ReportDelete204, reportSeedMissing));
            results.Add(MakeFailedResult(StepNames.ReportRestorePatch204, reportSeedMissing));
        }

        var fakeScheduleId = Guid.NewGuid().ToString();
        results.Add(await CallBffAsync(StepNames.ReportDelete404, $"{baseUrl}/aggregate/reports/{fakeScheduleId}", "DELETE", 404, ct));
        results.Add(await CallBffAsync(StepNames.ReportRestorePatch404, $"{baseUrl}/aggregate/reports/{fakeScheduleId}/restore", "PATCH", 404, ct));

        return results;
    }

    /// <summary>
    /// Creates a facility through the BFF's YARP proxy to the Tenant service.
    /// Returns true if the facility was created successfully.
    /// </summary>
    private async Task<bool> CreateFacilityViaBffAsync(string baseUrl, string facilityId, CancellationToken ct)
    {
        try
        {
            var body = JsonSerializer.Serialize(new
            {
                facilityId,
                facilityName = facilityId,
                timeZone = "America/Chicago",
                vendor = "Epic",
                scheduledReports = new { daily = Array.Empty<string>(), weekly = Array.Empty<string>(), monthly = Array.Empty<string>() }
            });
            var response = await CallBffRawAsync($"{baseUrl}/Facility", "POST", ct, body);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create prerequisite facility {FacilityId} via BFF", facilityId);
            return false;
        }
    }


    private async Task<HttpResponseMessage> CallBffRawAsync(string url, string method, CancellationToken ct, string? jsonBody = null)
    {
        var client = _httpClientFactory.CreateClient("ApiHealthTest");
        client.Timeout = TimeSpan.FromSeconds(30);

        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (jsonBody != null)
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        if (_bearerOptions.Value?.AllowAnonymous != true)
        {
            var signingKey = _tokenSettings.Value?.SigningKey;
            if (!string.IsNullOrWhiteSpace(signingKey))
            {
                var token = await _tokenService.ExecuteAsync(signingKey, 5);
                if (!string.IsNullOrWhiteSpace(token))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        return await client.SendAsync(request, ct);
    }

    private async Task<ApiTestRunResult> CallBffAsync(string endpointName, string url, string method, int expectedStatus, CancellationToken ct)
    {
        var result = new ApiTestRunResult
        {
            EndpointKey = $"{ServiceName}::{endpointName}",
            ServiceName = ServiceName,
            EndpointName = endpointName,
            ExpectedStatusCode = expectedStatus,
            ExecutedAt = DateTimeOffset.UtcNow,
            RequestUrl = url,
            RequestMethod = method,
            RequestBody = $"No request body was sent ({method.ToUpperInvariant()})."
        };

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await CallBffRawAsync(url, method, ct);
            sw.Stop();

            result.ActualStatusCode = (int)response.StatusCode;
            result.DurationMs = sw.ElapsedMilliseconds;
            result.Passed = result.ActualStatusCode == expectedStatus;

            // Extract trace ID from response headers
            if (response.Headers.TryGetValues("traceparent", out var traceValues))
                result.TraceId = traceValues.FirstOrDefault();
            else if (response.Headers.TryGetValues("X-Trace-Id", out var xTraceValues))
                result.TraceId = xTraceValues.FirstOrDefault();

            // Capture response body for diagnostics
            try
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                var capturedBody = body.Length > 500 ? body[..500] : body;
                result.ResponseBody = string.IsNullOrWhiteSpace(capturedBody)
                    ? $"No response body was returned (HTTP {result.ActualStatusCode})."
                    : capturedBody;
                if (!result.Passed)
                    result.ErrorMessage = BuildStatusMismatchMessage(expectedStatus, result.ActualStatusCode ?? 0, result.ResponseBody, result.TraceId);
            }
            catch
            {
                result.ResponseBody = $"Response body could not be read (HTTP {result.ActualStatusCode}).";
                if (!result.Passed)
                    result.ErrorMessage = BuildStatusMismatchMessage(expectedStatus, result.ActualStatusCode ?? 0, result.ResponseBody, result.TraceId);
            }
        }
        catch (TaskCanceledException)
        {
            sw.Stop();
            result.DurationMs = sw.ElapsedMilliseconds;
            result.Passed = false;
            result.ErrorMessage = "Request timed out.";
            result.ResponseBody = "No response body was received because the request timed out.";
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            result.DurationMs = sw.ElapsedMilliseconds;
            result.Passed = false;
            result.ErrorMessage = $"HTTP error: {ex.Message}";
            result.ResponseBody = "No response body was received because the HTTP request failed.";
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.DurationMs = sw.ElapsedMilliseconds;
            result.Passed = false;
            result.ErrorMessage = $"Unexpected error: {ex.Message}";
            result.ResponseBody = Truncate(ex.ToString());
            _logger.LogError(ex, "Admin BFF test failed for {Endpoint}", endpointName);
        }

        return result;
    }

    private ApiTestRunResult MakeFailedResult(string endpointName, string error) => new()
    {
        EndpointKey = $"{ServiceName}::{endpointName}",
        ServiceName = ServiceName,
        EndpointName = endpointName,
        Passed = false,
        ErrorMessage = error,
        RequestBody = "Request was not sent because prerequisite setup failed.",
        ResponseBody = "Response was not received because prerequisite setup failed.",
        ExecutedAt = DateTimeOffset.UtcNow
    };

    private static string Truncate(string? value, int maxLength = 300) =>
        value == null ? "" : (value.Length > maxLength ? value[..maxLength] : value);

    private static string BuildStatusMismatchMessage(int expectedStatus, int actualStatus, string? responseBody, string? traceId)
    {
        var baseMessage = $"Error: Expected HTTP {expectedStatus} but got {actualStatus}.";
        if (actualStatus != 500)
            return baseMessage;

        var parts = new List<string> { baseMessage };

        var apiResponse = ExtractApiResponseMessage(responseBody);
        if (!string.IsNullOrWhiteSpace(apiResponse))
            parts.Add($"Detail: {apiResponse}");

        if (!string.IsNullOrWhiteSpace(traceId))
            parts.Add($"Trace ID: {traceId}");

        return string.Join(Environment.NewLine, parts);
    }

    private static string? ExtractApiResponseMessage(string? rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
            return null;

        var trimmed = Truncate(rawBody).Trim();

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return trimmed;

            string? TryGet(string propertyName)
                => doc.RootElement.TryGetProperty(propertyName, out var p) && p.ValueKind == JsonValueKind.String
                    ? p.GetString()
                    : null;

            var message = TryGet("error")
                ?? TryGet("message")
                ?? TryGet("title")
                ?? TryGet("detail");

            return string.IsNullOrWhiteSpace(message) ? trimmed : message.Trim();
        }
        catch
        {
            return trimmed;
        }
    }

}
