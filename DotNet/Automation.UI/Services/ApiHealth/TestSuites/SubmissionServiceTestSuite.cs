using Automation.UI.Models.ApiHealth;
using Automation.UI.Services.ApiHealth.Seeding;
using LantanaGroup.Link.Sdk.Clients;
using StepNames = Automation.UI.Services.ApiHealth.TestSuites.ApiEndPointLibrary.SubmissionSteps;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Automation.UI.Services.ApiHealth.TestSuites;

/// <summary>
/// Exercises Submission service operations via LinkSdk:
///   1. Download Submission → 404 (proves reachability with non-existent resource)
///   2. Download Submission → 400 (empty/whitespace facilityId)
///   3. Download Submission → 400 (empty/whitespace reportId)
///
/// Note: A 200 test requires a fully-submitted report in blob storage.
/// This is inherently not self-contained without running a full pipeline.
/// The 404 test proves the service is reachable and the 400 tests validate both
/// independent input guards in the controller.
/// </summary>
public sealed class SubmissionServiceTestSuite : ServiceTestSuiteBase
{
    private readonly ISubmissionServiceClient _client;
    private readonly IApiHealthSeedContextAccessor _seedContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<ServiceRegistry> _serviceRegistry;

    public override string ServiceName => "Submission";

    public SubmissionServiceTestSuite(
        ISubmissionServiceClient client,
        IApiHealthSeedContextAccessor seedContext,
        IHttpClientFactory httpClientFactory,
        IOptions<ServiceRegistry> serviceRegistry)
    {
        _client = client;
        _seedContext = seedContext;
        _httpClientFactory = httpClientFactory;
        _serviceRegistry = serviceRegistry;
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

        var baseUrl = _serviceRegistry.Value.SubmissionServiceUrl?.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            const string error =
                "ServiceRegistry:SubmissionServiceUrl is not configured.";

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
                        "Request was not sent because the Submission service URL is missing.",
                    ResponseBody =
                        "Response was not received because the Submission service URL is missing.",
                    ExecutedAt = DateTimeOffset.UtcNow
                });
            }
        }
        else
        {
            results.Add(await CallRawGetAsync(
                StepNames.InfoGet200,
                baseUrl,
                "/api/Submission/info",
                ct));

            results.Add(await CallRawGetAsync(
                StepNames.RootHealthGet200,
                baseUrl,
                "/health",
                ct));
        }

        var fakeFacilityId = $"ApiHealth-Sub-{Guid.NewGuid():N}";
        var fakeReportId = Guid.NewGuid().ToString();

        results.Add(await RunSeededSuccessStepAsync(
            StepNames.Get200,
            ct));

        results.Add(await RunStepAsync(
            StepNames.Get400BadReportId,
            400,
            async () =>
                await _client.DownloadSubmissionAsync(
                    fakeFacilityId,
                    "not-a-valid-guid",
                    cancellationToken: ct),
            ct: ct));

        results.Add(await RunStepAsync(
            StepNames.Get404NotFound,
            404,
            async () =>
                await _client.DownloadSubmissionAsync(
                    fakeFacilityId,
                    fakeReportId,
                    cancellationToken: ct),
            ct: ct));

        results.Add(await RunStepAsync(
            StepNames.Get400EmptyFacilityId,
            400,
            async () =>
                await _client.DownloadSubmissionAsync(
                    " ",
                    fakeReportId,
                    cancellationToken: ct),
            ct: ct));

        results.Add(await RunStepAsync(
            StepNames.Get400EmptyReportId,
            400,
            async () =>
                await _client.DownloadSubmissionAsync(
                    fakeFacilityId,
                    " ",
                    cancellationToken: ct),
            ct: ct));

        return results;
    }

    private async Task<ApiTestRunResult> RunSeededSuccessStepAsync(string stepName, CancellationToken ct)
    {
        var seeded = _seedContext.Current?.Report;
        if (seeded is not { FacilityId: { Length: > 0 } facilityId, ScheduleId: { Length: > 0 } reportId })
        {
            return SkipStepAsync(
                stepName,
                "Submission seed was unavailable. This test requires API-health seeded facility/report identifiers.");
        }

        return await RunStepAsync(stepName, 200, async () =>
            await _client.DownloadSubmissionAsync(facilityId, reportId, cancellationToken: ct), ct: ct);
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
