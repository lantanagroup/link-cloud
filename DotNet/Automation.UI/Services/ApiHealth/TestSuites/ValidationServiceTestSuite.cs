using Automation.UI.Models.ApiHealth;
using Automation.UI.Services.ApiHealth.Seeding;
using LantanaGroup.Link.Sdk.Clients;
using StepNames = Automation.UI.Services.ApiHealth.TestSuites.ApiEndPointLibrary.ValidationSteps;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Automation.UI.Services.ApiHealth.TestSuites;

/// <summary>
/// Exercises Validation service operations via LinkSdk:
///   1. Has Artifacts (check if initialized)
///   2. Has Categories (check if initialized)
///   3. Upsert Resource Artifact
///   4. Get Validation Results (non-existent — proves reachability)
///
/// NOTE: Validation is a Java service. InitializeArtifacts/InitializeCategories
/// are only called if not already initialized, to avoid disrupting existing state.
/// </summary>
public sealed class ValidationServiceTestSuite : ServiceTestSuiteBase
{
    private readonly IValidationServiceClient _client;
    private readonly IApiHealthSeedContextAccessor _seedContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<ServiceRegistry> _serviceRegistry;

    public override string ServiceName => "Validation";
    public ValidationServiceTestSuite(
        IValidationServiceClient client,
        IApiHealthSeedContextAccessor seedContext,
        IHttpClientFactory httpClientFactory,
        IOptions<ServiceRegistry> serviceRegistry)
    {
        _client = client;
        _seedContext = seedContext;
        _httpClientFactory = httpClientFactory;
        _serviceRegistry = serviceRegistry;
    }

    public override IReadOnlyList<ApiHealthSeedRequirement> GetSeedRequirements() =>
    [
        ApiHealthSeedRequirement.ReportSchedule
    ];

    public override IReadOnlyList<ApiEndpointDefinition> GetEndpointDefinitions() =>
        ApiEndPointLibrary.GetServiceEndpoints(ServiceName);

    public override async Task<IReadOnlyList<ApiTestRunResult>> ExecuteAsync(CancellationToken ct = default)
    {
        var results = new List<ApiTestRunResult>();

        var baseUrl = _serviceRegistry.Value.ValidationServiceUrl?.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            const string error =
                "ServiceRegistry:ValidationServiceUrl is not configured.";

            foreach (var endpointName in new[]
            {
                StepNames.InfoGet200,
                StepNames.RootHealthGet200
            })
            {
                results.Add(new ApiTestRunResult
                {
                    EndpointKey = $"{ServiceName}::{endpointName}",
                    ServiceName = ServiceName,
                    EndpointName = endpointName,
                    Passed = false,
                    ExpectedStatusCode = 200,
                    ErrorMessage = error,
                    RequestBody =
                        "Request was not sent because the Validation service URL is missing.",
                    ResponseBody =
                        "Response was not received because the Validation service URL is missing.",
                    ExecutedAt = DateTimeOffset.UtcNow
                });
            }
        }
        else
        {
            results.Add(await CallRawGetAsync(
                StepNames.InfoGet200,
                baseUrl,
                "/api/Validation/info",
                ct));

            results.Add(await CallRawGetAsync(
                StepNames.RootHealthGet200,
                baseUrl,
                "/health",
                ct));
        }

        var fakeFacilityId = $"ApiHealth-Val-{Guid.NewGuid():N}";

        results.Add(await RunStepAsync(
            StepNames.ArtifactsGet200,
            200,
            async () => await _client.GetArtifactsAsync(ct),
            ct: ct));

        results.Add(await RunStepAsync(
            StepNames.CategoriesGet200,
            200,
            async () => await _client.GetCategoriesAsync(ct),
            ct: ct));

        results.Add(await RunStepAsync(
            StepNames.ArtifactPut200Or201,
            [200, 201],
            async () =>
            {
                var artifactId = $"OperationOutcome-{Guid.NewGuid():N}";
                var payload = $$"""
            {
                "resourceType": "OperationOutcome",
                "id": "{{Guid.NewGuid():N}}",
                "issue": [{
                    "severity": "information",
                    "code": "informational",
                    "diagnostics": "ApiHealth stability test artifact"
                }]
            }
            """;

                return await _client.UpsertResourceArtifactAsync(
                    artifactId,
                    payload,
                    ct);
            },
            ct: ct));

        results.Add(await RunSeededResultsStepAsync(
            StepNames.ResultsGet200Seeded,
            ct));

        results.Add(await RunStepAsync(
            StepNames.ResultsGet200Empty,
            200,
            async () =>
                await _client.GetValidationResultsAsync(
                    fakeFacilityId,
                    Guid.NewGuid().ToString(),
                    cancellationToken: ct),
            ct: ct));

        return results;
    }

    private async Task<ApiTestRunResult> RunSeededResultsStepAsync(string stepName, CancellationToken ct)
    {
        var seeded = _seedContext.Current?.Report;
        if (seeded is not { FacilityId: { Length: > 0 } facilityId, ScheduleId: { Length: > 0 } reportId })
            return SkipStepAsync(stepName, "Validation seeded result check requires seeded facility/report identifiers.");

        return await RunStepAsync(stepName, 200, async () =>
            await _client.GetValidationResultsAsync(facilityId, reportId, cancellationToken: ct), ct: ct);
    }

    private async Task<ApiTestRunResult> CallRawGetAsync(
        string endpointName,
        string baseUrl,
        string relativePath,
        CancellationToken ct)
    {
        var result = new ApiTestRunResult
        {
            EndpointKey = $"{ServiceName}::{endpointName}",
            ServiceName = ServiceName,
            EndpointName = endpointName,
            ExpectedStatusCode = 200,
            ExecutedAt = DateTimeOffset.UtcNow,
            RequestMethod = "GET",
            RequestUrl = $"{baseUrl}{relativePath}",
            RequestBody = "No request body was sent (GET)."
        };

        var sw = Stopwatch.StartNew();

        try
        {
            ct.ThrowIfCancellationRequested();

            var httpClient =
                _httpClientFactory.CreateClient("ApiHealthTest");

            using var response = await httpClient.GetAsync(
                $"{baseUrl}{relativePath}",
                ct);

            var responseBody =
                await response.Content.ReadAsStringAsync(ct);

            sw.Stop();

            result.ActualStatusCode = (int)response.StatusCode;
            result.DurationMs = sw.ElapsedMilliseconds;
            result.Passed = result.ActualStatusCode == 200;

            result.ResponseBody = string.IsNullOrWhiteSpace(responseBody)
                ? $"No response body was returned (HTTP {result.ActualStatusCode})."
                : responseBody.Length > 500
                    ? responseBody[..500]
                    : responseBody;

            if (!result.Passed)
            {
                result.ErrorMessage =
                    $"Expected HTTP 200 but got {result.ActualStatusCode}.";
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            sw.Stop();
            result.DurationMs = sw.ElapsedMilliseconds;
            result.Passed = false;
            result.ErrorMessage = "Request timed out.";
            result.ResponseBody =
                "No response body was received because the request timed out.";
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            result.DurationMs = sw.ElapsedMilliseconds;
            result.Passed = false;
            result.ErrorMessage = $"HTTP error: {ex.Message}";
            result.ResponseBody =
                "No response body was received because the HTTP request failed.";
        }

        return result;
    }
}
