using Automation.UI.Models.ApiHealth;
using Automation.UI.Services.ApiHealth.Seeding;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;
using StepNames = Automation.UI.Services.ApiHealth.TestSuites.ApiEndPointLibrary.AdminBffSteps;

namespace Automation.UI.Services.ApiHealth.TestSuites;

/// <summary>
/// Exercises the Admin BFF's unique aggregation endpoints via HTTP.
/// Tests both success (2xx) and error (4xx) paths for each endpoint.
/// Creates prerequisite data (facility) to exercise the full soft-delete/restore lifecycle.
/// </summary>
public sealed class AdminBffTestSuite : ServiceTestSuiteBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<ServiceRegistry> _serviceRegistry;
    private readonly IApiHealthSeedContextAccessor _seedContext;
    private readonly ILogger<AdminBffTestSuite> _logger;

    public override string ServiceName => "AdminBff";

    public AdminBffTestSuite(
        IServiceProvider serviceProvider,
        IOptions<ServiceRegistry> serviceRegistry,
        IApiHealthSeedContextAccessor seedContext,
        ILogger<AdminBffTestSuite> logger)
    {
        _serviceProvider = serviceProvider;
        _serviceRegistry = serviceRegistry;
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
        var baseUrl = _serviceRegistry.Value.AdminBffServiceApiUrl?.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            results.Add(new ApiTestRunResult
            {
                EndpointKey = $"{ServiceName}::{StepNames.HealthGet200}",
                ServiceName = ServiceName,
                Passed = false,
                ErrorMessage = "ServiceRegistry:AdminBffServiceUrl is not configured.",
                RequestBody = "Request was not sent because ServiceRegistry:AdminBffServiceUrl is missing.",
                ResponseBody = "Response was not received because ServiceRegistry:AdminBffServiceUrl is missing."
            });
            return results;
        }

        var adminBffClient = _serviceProvider.GetRequiredService<IAdminBffIntegrationClient>();

        // --- /api/monitor/health ---
        results.Add(await CallBffAsync(StepNames.HealthGet200, "GET", 200, () => adminBffClient.GetHealthAsync(ct), ct));

        // --- /api/aggregate/facility/{id} lifecycle ---
        var facilityId = $"ApiHealth-BFF-{Guid.NewGuid():N}";
        var facilityCreated = false;

        try
        {
            var createResult = await CreateFacilityViaBffAsync(adminBffClient, facilityId, ct);
            facilityCreated = createResult;

            if (facilityCreated)
            {
                results.Add(await CallBffAsync(StepNames.FacilityDelete200, "DELETE", 200,
                    () => adminBffClient.SoftDeleteAggregateFacilityAsync(facilityId, ct), ct));
                results.Add(await CallBffAsync(StepNames.FacilityRestorePatch200, "PATCH", 200,
                    () => adminBffClient.RestoreAggregateFacilityAsync(facilityId, ct), ct));
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
                try
                {
                    using var cleanupCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await adminBffClient.DeleteFacilityAsync(facilityId, cleanupCts.Token);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Best-effort cleanup failed deleting facility {FacilityId} in Admin BFF API health suite.",
                        facilityId);
                }
            }
        }

        var fakeFacilityId = $"ApiHealth-BFF-{Guid.NewGuid():N}";
        results.Add(await CallBffAsync(StepNames.FacilityDelete404, "DELETE", 404,
            () => adminBffClient.SoftDeleteAggregateFacilityAsync(fakeFacilityId, ct), ct));
        results.Add(await CallBffAsync(StepNames.FacilityRestorePatch404, "PATCH", 404,
            () => adminBffClient.RestoreAggregateFacilityAsync(fakeFacilityId, ct), ct));

        // --- /api/aggregate/reports/summaries ---
        results.Add(await CallBffAsync(StepNames.SummariesGet200, "GET", 200,
            () => adminBffClient.GetReportSummariesAsync(ct), ct));

        // Seed-owned only: this suite requires ReportSchedule seed fixture.
        var reportScheduleId = _seedContext.Current?.Report?.ScheduleId;
        const string reportSeedMissing = "Report seed was unavailable. This lifecycle test requires seed-owned schedule data from the seeding phase.";

        // --- /api/aggregate/reports/summaries/{id} ---
        if (!string.IsNullOrWhiteSpace(reportScheduleId))
            results.Add(await CallBffAsync(StepNames.SummaryGet200, "GET", 200,
                () => adminBffClient.GetReportSummaryAsync(reportScheduleId, ct), ct));
        else
            results.Add(SkipStepAsync(StepNames.SummaryGet200, reportSeedMissing));

        var fakeReportId = Guid.NewGuid().ToString();
        results.Add(await CallBffAsync(StepNames.SummaryGet404, "GET", 404,
            () => adminBffClient.GetReportSummaryAsync(fakeReportId, ct), ct));

        // --- /api/aggregate/reports/{id} lifecycle ---

        if (!string.IsNullOrWhiteSpace(reportScheduleId))
        {
            results.Add(await CallBffAsync(StepNames.ReportDelete204, "DELETE", 204,
                () => adminBffClient.DeleteAggregateReportAsync(reportScheduleId, ct), ct));
            results.Add(await CallBffAsync(StepNames.ReportRestorePatch204, "PATCH", 204,
                () => adminBffClient.RestoreAggregateReportAsync(reportScheduleId, ct), ct));
        }
        else
        {
            results.Add(MakeFailedResult(StepNames.ReportDelete204, reportSeedMissing));
            results.Add(MakeFailedResult(StepNames.ReportRestorePatch204, reportSeedMissing));
        }

        var fakeScheduleId = Guid.NewGuid().ToString();
        results.Add(await CallBffAsync(StepNames.ReportDelete404, "DELETE", 404,
            () => adminBffClient.DeleteAggregateReportAsync(fakeScheduleId, ct), ct));
        results.Add(await CallBffAsync(StepNames.ReportRestorePatch404, "PATCH", 404,
            () => adminBffClient.RestoreAggregateReportAsync(fakeScheduleId, ct), ct));

        return results;
    }

    /// <summary>
    /// Creates a facility through the BFF's YARP proxy to the Tenant service.
    /// Returns true if the facility was created successfully.
    /// </summary>
    private async Task<bool> CreateFacilityViaBffAsync(IAdminBffIntegrationClient adminBffClient, string facilityId, CancellationToken ct)
    {
        try
        {
            var body = new FacilityModel
            {
                FacilityId = facilityId,
                FacilityName = facilityId,
                TimeZone = "America/Chicago",
                Vendor = new VendorModel
                {
                    Name = "Epic"
                },
                ScheduledReports = new TenantScheduledReportConfig
                {
                    Daily = [],
                    Weekly = [],
                    Monthly = []
                }
            };
            var response = await adminBffClient.CreateFacilityAsync(body, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create prerequisite facility {FacilityId} via BFF", facilityId);
            return false;
        }
    }


    private async Task<ApiTestRunResult> CallBffAsync(
        string endpointName,
        string method,
        int expectedStatus,
        Func<Task<LinkApiResponse>> send,
        CancellationToken ct)
    {
        var result = new ApiTestRunResult
        {
            EndpointKey = $"{ServiceName}::{endpointName}",
            ServiceName = ServiceName,
            EndpointName = endpointName,
            ExpectedStatusCode = expectedStatus,
            ExecutedAt = DateTimeOffset.UtcNow,
            RequestMethod = method,
            RequestBody = $"No request body was sent ({method.ToUpperInvariant()})."
        };

        var sw = Stopwatch.StartNew();
        try
        {
            ct.ThrowIfCancellationRequested();
            var response = await send();
            sw.Stop();

            result.ActualStatusCode = response.StatusCode;
            result.DurationMs = sw.ElapsedMilliseconds;
            result.Passed = result.ActualStatusCode == expectedStatus;
            result.RequestUrl = response.RequestUrl;
            result.TraceId = response.TraceId;

            var body = response.RawBody;
            var capturedBody = string.IsNullOrWhiteSpace(body)
                ? $"No response body was returned (HTTP {result.ActualStatusCode})."
                : (body.Length > 500 ? body[..500] : body);
            result.ResponseBody = capturedBody;
            if (!result.Passed)
                result.ErrorMessage = BuildStatusMismatchMessage(expectedStatus, result.ActualStatusCode ?? 0, result.ResponseBody, result.TraceId);
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == ct || ct.IsCancellationRequested)
        {
            throw;
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
            result.ResponseBody = ex.ToString();
            _logger.LogError(ex, "Admin BFF test failed for {Endpoint}", endpointName);
        }

        return result;
    }

    private Task<ApiTestRunResult> CallBffAsync(
        string endpointName,
        string method,
        int expectedStatus,
        Func<Task<LinkApiResponse<string>>> send,
        CancellationToken ct)
    {
        Func<Task<LinkApiResponse>> sendUntyped = async () => (await send()).AsUntyped();
        return CallBffAsync(endpointName, method, expectedStatus, sendUntyped, ct);
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

        var trimmed = rawBody.Trim();

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
