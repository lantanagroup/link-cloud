using Automation.UI.Models.ApiHealth;
using LantanaGroup.Link.Sdk.Clients;
using System.Text.Json;
using StepNames = Automation.UI.Services.ApiHealth.TestSuites.ApiEndPointLibrary.MeasureEvalSteps;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Automation.UI.Services.ApiHealth.TestSuites;

/// <summary>
/// Exercises MeasureEval service operations via LinkSdk.
///
/// NOTE: MeasureEval is a Java service. All error paths return 400 — there is no 422.
/// FhirParseException, ValidationException, and ResponseStatusException are all
/// mapped to 400 by ExceptionHandlers.java.
///
/// Two distinct PUT 400 paths are covered:
///   1. Malformed body ("{}") — HAPI-FHIR cannot deserialize → FhirParseException → 400.
///   2. Valid FHIR Bundle with an id but no Measure resource — passes deserialization
///      and the id guard, then MeasureDefinitionBundleValidator throws ValidationException → 400.
///
/// A 200 test for PUT requires a complete CQL Bundle and is not self-contained.
/// </summary>
public sealed class MeasureEvalTestSuite : ServiceTestSuiteBase
{
    private readonly IMeasureEvalServiceClient _client;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<ServiceRegistry> _serviceRegistry;

    private static readonly string NoMeasureBundleJson = """
        {
          "resourceType": "Bundle",
          "id": "ApiHealth-MeasureEval-NoMeasure",
          "type": "collection",
          "entry": []
        }
        """;

    public override string ServiceName => "MeasureEval";
    public MeasureEvalTestSuite(
        IMeasureEvalServiceClient client,
        IHttpClientFactory httpClientFactory,
        IOptions<ServiceRegistry> serviceRegistry)
    {
        _client = client;
        _httpClientFactory = httpClientFactory;
        _serviceRegistry = serviceRegistry;
    }

    public override IReadOnlyList<ApiEndpointDefinition> GetEndpointDefinitions() =>
        ApiEndPointLibrary.GetServiceEndpoints(ServiceName);

    public override async Task<IReadOnlyList<ApiTestRunResult>> ExecuteAsync(CancellationToken ct = default)
    {
        var results = new List<ApiTestRunResult>();

        var baseUrl = _serviceRegistry.Value.MeasureServiceUrl?.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            const string error =
                "ServiceRegistry:MeasureServiceUrl is not configured.";

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
                        "Request was not sent because the MeasureEval service URL is missing.",
                    ResponseBody =
                        "Response was not received because the MeasureEval service URL is missing.",
                    ExecutedAt = DateTimeOffset.UtcNow
                });
            }
        }
        else
        {
            results.Add(await CallRawGetAsync(
                StepNames.InfoGet200,
                baseUrl,
                "/api/MeasureEval/info",
                ct));

            results.Add(await CallRawGetAsync(
                StepNames.RootHealthGet200,
                baseUrl,
                "/health",
                ct));
        }

        results.Add(await RunStepAsync(
            StepNames.GetAll200,
            200,
            async () => await _client.GetAllMeasureDefinitionsAsync(ct),
            ct: ct));

        results.Add(await RunGetByIdSuccessStepAsync(
            StepNames.Get200,
            ct));

        results.Add(await RunStepAsync(
            StepNames.Get404,
            404,
            async () =>
                await _client.GetMeasureDefinitionAsync(
                    $"ApiHealth-Measure-{Guid.NewGuid():N}",
                    ct),
            ct: ct));

        results.Add(await RunStepAsync(
            StepNames.Put400MalformedBody,
            400,
            async () =>
                await _client.PutMeasureDefinitionAsync("{}", ct),
            ct: ct));

        results.Add(await RunStepAsync(
            StepNames.Put400NoMeasureInBundle,
            400,
            async () =>
                await _client.PutMeasureDefinitionAsync(
                    NoMeasureBundleJson,
                    ct),
            ct: ct));

        return results;
    }

    private async Task<ApiTestRunResult> RunGetByIdSuccessStepAsync(string stepName, CancellationToken ct)
    {
        var allMeasures = await _client.GetAllMeasureDefinitionsAsync(ct);
        var existingMeasureId = TryExtractFirstMeasureId(allMeasures.Body);
        if (string.IsNullOrWhiteSpace(existingMeasureId))
            return SkipStepAsync(stepName, "No measure definitions were available to validate the GET-by-id 200 path.");

        return await RunStepAsync(stepName, 200, async () =>
            await _client.GetMeasureDefinitionAsync(existingMeasureId, ct), ct: ct);
    }

    private static string? TryExtractFirstMeasureId(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                    return item.GetString();

                if (item.ValueKind == JsonValueKind.Object)
                {
                    if (item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                        return id.GetString();
                    if (item.TryGetProperty("measureId", out var measureId) && measureId.ValueKind == JsonValueKind.String)
                        return measureId.GetString();
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
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
