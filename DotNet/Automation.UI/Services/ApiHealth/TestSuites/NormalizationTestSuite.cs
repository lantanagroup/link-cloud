using Automation.UI.Models.ApiHealth;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using StepNames = Automation.UI.Services.ApiHealth.TestSuites.ApiEndPointLibrary.NormalizationSteps;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Automation.UI.Services.ApiHealth.TestSuites;

/// <summary>
/// Exercises Normalization service CRUD operations via LinkSdk.
/// Self-contained: creates its own prerequisite facility for each run.
/// Includes SDK-reachable 4xx validation paths for malformed create/search/sequence requests.
/// </summary>
public sealed class NormalizationTestSuite : ServiceTestSuiteBase
{
    private readonly INormalizationServiceClient _client;
    private readonly IFacilityServiceClient _facilityClient;
    private readonly ILogger<NormalizationTestSuite> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<ServiceRegistry> _serviceRegistry;

    public override string ServiceName => "Normalization";
    public NormalizationTestSuite(
        INormalizationServiceClient client,
        IFacilityServiceClient facilityClient,
        ILogger<NormalizationTestSuite> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<ServiceRegistry> serviceRegistry)
    {
        _client = client;
        _facilityClient = facilityClient;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _serviceRegistry = serviceRegistry;
    }

    public override IReadOnlyList<ApiEndpointDefinition> GetEndpointDefinitions() =>
        ApiEndPointLibrary.GetServiceEndpoints(ServiceName);

    public override async Task<IReadOnlyList<ApiTestRunResult>> ExecuteAsync(CancellationToken ct = default)
    {
        var results = new List<ApiTestRunResult>();

        var baseUrl = _serviceRegistry.Value.NormalizationServiceUrl?.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            const string error =
                "ServiceRegistry:NormalizationServiceUrl is not configured.";

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
                        "Request was not sent because the Normalization service URL is missing.",
                    ResponseBody =
                        "Response was not received because the Normalization service URL is missing.",
                    ExecutedAt = DateTimeOffset.UtcNow
                });
            }
        }
        else
        {
            results.Add(await CallRawGetAsync(
                StepNames.InfoGet200,
                baseUrl,
                "/api/Normalization/info",
                ct));

            results.Add(await CallRawGetAsync(
                StepNames.RootHealthGet200,
                baseUrl,
                "/health",
                ct));
        }

        var facilityId = $"ApiHealth-Norm-{Guid.NewGuid():N}";
        var locationId = $"ApiHealth-Location-{Guid.NewGuid():N}";
        var localCodeSystem = $"urn:api-health:{Guid.NewGuid():N}";
        var localCode = $"ApiHealth-Code-{Guid.NewGuid():N}";
        var facilityCreated = false;
        var facilityLocationCreated = false;
        var operationCreated = false;

        try
        {
            // Prerequisite: create facility (infrastructure, not tracked)
            await CreateFacilityAsync(facilityId, ct);
            facilityCreated = true;

            results.Add(await RunStepAsync(StepNames.FacilityLocationPost400EmptyLocationId, 400, async () =>
                await _client.CreateFacilityLocationAsync(facilityId, new CreateFacilityLocationRequestApiModel
                {
                    LocationId = " ",
                    LocationName = "ApiHealth Invalid Facility Location"
                }, ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.FacilityLocationPost201, 201, async () =>
            {
                var response = await _client.CreateFacilityLocationAsync(facilityId, new CreateFacilityLocationRequestApiModel
                {
                    LocationId = locationId,
                    LocationName = "ApiHealth Facility Location",
                    LocationAlias = "api-health-location"
                }, ct);
                if (response.IsSuccessStatusCode)
                {
                    facilityLocationCreated = true;
                }

                return response;
            }, ct: ct));

            results.Add(await RunStepAsync(StepNames.FacilityLocationPost409Duplicate, 409, async () =>
                await _client.CreateFacilityLocationAsync(facilityId, new CreateFacilityLocationRequestApiModel
                {
                    LocationId = locationId,
                    LocationName = "ApiHealth Duplicate Facility Location"
                }, ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.FacilityLocationGet200, 200, async () =>
            {
                var response = await _client.GetFacilityLocationAsync(facilityId, locationId, ct);
                if (response.IsSuccessStatusCode && response.Body?.LocationId != locationId)
                {
                    throw new InvalidOperationException("The returned facility location did not match the created fixture.");
                }

                return response;
            }, ct: ct));

            results.Add(await RunStepAsync(StepNames.FacilityLocationGet404, 404, async () =>
                await _client.GetFacilityLocationAsync(facilityId, $"ApiHealth-Missing-{Guid.NewGuid():N}", ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.HslocMappingSearch200InitialEmpty, 200, async () =>
            {
                var response = await _client.SearchFacilityLocationLocalCodeMappingsAsync(
                    new SearchFacilityLocationLocalCodeMappingsRequestApiModel
                    {
                        FacilityId = facilityId,
                        PageSize = 10,
                        PageNumber = 1
                    },
                    ct);
                if (response.IsSuccessStatusCode && response.Body?.Records is { Count: > 0 })
                {
                    throw new InvalidOperationException("Expected no local-code mappings before the test fixture is created.");
                }

                return response;
            }, ct: ct));

            results.Add(await RunStepAsync(StepNames.HslocMappingPost400EmptyLocationId, 400, async () =>
                await _client.CreateFacilityLocationLocalCodeMappingAsync(facilityId, new CreateFacilityLocationLocalCodeMappingRequestApiModel
                {
                    LocationId = " ",
                    LocalCodeSystem = localCodeSystem,
                    LocalCode = localCode
                }, ct), ct: ct));

            string? mappingId = null;
            results.Add(await RunStepAsync(StepNames.HslocMappingPost201, 201, async () =>
            {
                var response = await _client.CreateFacilityLocationLocalCodeMappingAsync(facilityId, new CreateFacilityLocationLocalCodeMappingRequestApiModel
                {
                    LocationId = locationId,
                    LocalCodeSystem = localCodeSystem,
                    LocalCode = localCode
                }, ct);
                mappingId = response.Body?.Id;
                return response;
            }, ct: ct));

            results.Add(await RunStepAsync(StepNames.HslocMappingPost409Duplicate, 409, async () =>
                await _client.CreateFacilityLocationLocalCodeMappingAsync(facilityId, new CreateFacilityLocationLocalCodeMappingRequestApiModel
                {
                    LocationId = locationId,
                    LocalCodeSystem = localCodeSystem,
                    LocalCode = localCode
                }, ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.HslocMappingSearch200, 200, async () =>
            {
                var response = await _client.SearchFacilityLocationLocalCodeMappingsAsync(
                    new SearchFacilityLocationLocalCodeMappingsRequestApiModel
                    {
                        FacilityId = facilityId,
                        LocationId = locationId,
                        LocalCodeSystem = localCodeSystem,
                        LocalCode = localCode,
                        PageSize = 10,
                        PageNumber = 1
                    },
                    ct);
                if (response.IsSuccessStatusCode && response.Body?.Records is not { Count: > 0 })
                {
                    throw new InvalidOperationException("Expected the created local-code mapping in the search response.");
                }

                return response;
            }, ct: ct));

            results.Add(await RunStepAsync(StepNames.HslocMappingGet200, 200, async () =>
            {
                if (string.IsNullOrWhiteSpace(mappingId))
                {
                    throw new InvalidOperationException("Expected a mapping identifier after creating the fixture.");
                }

                return await _client.GetFacilityLocationLocalCodeMappingAsync(mappingId, ct);
            }, ct: ct));

            results.Add(await RunStepAsync(StepNames.HslocMappingPut202, 202, async () =>
            {
                if (string.IsNullOrWhiteSpace(mappingId))
                {
                    throw new InvalidOperationException("Expected a mapping identifier before updating the fixture.");
                }

                return await _client.UpdateFacilityLocationLocalCodeMappingAsync(mappingId, new UpdateFacilityLocationLocalCodeMappingRequestApiModel
                {
                    LocalCodeSystem = localCodeSystem,
                    LocalCode = $"{localCode}-updated"
                }, ct);
            }, ct: ct));

            results.Add(await RunStepAsync(StepNames.HslocMappingDelete204, 204, async () =>
            {
                if (string.IsNullOrWhiteSpace(mappingId))
                {
                    throw new InvalidOperationException("Expected a mapping identifier before deleting the fixture.");
                }

                return await _client.DeleteFacilityLocationLocalCodeMappingAsync(mappingId, ct);
            }, ct: ct));

            results.Add(await RunStepAsync(StepNames.HslocMappingGet404, 404, async () =>
            {
                if (string.IsNullOrWhiteSpace(mappingId))
                {
                    throw new InvalidOperationException("Expected a mapping identifier after deleting the fixture.");
                }

                return await _client.GetFacilityLocationLocalCodeMappingAsync(mappingId, ct);
            }, ct: ct));

            results.Add(await RunStepAsync(StepNames.HslocMappingDeleteForFacility204, 204, async () =>
            {
                var createResponse = await _client.CreateFacilityLocationLocalCodeMappingAsync(facilityId, new CreateFacilityLocationLocalCodeMappingRequestApiModel
                {
                    LocationId = locationId,
                    LocalCodeSystem = localCodeSystem,
                    LocalCode = $"{localCode}-facility-delete"
                }, ct);
                if (!createResponse.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException("Expected a local-code mapping fixture before deleting mappings for the facility.");
                }

                return await _client.DeleteFacilityLocationLocalCodeMappingsForFacilityAsync(facilityId, ct);
            }, ct: ct));

            results.Add(await RunStepAsync(StepNames.HslocMappingSearch200Empty, 200, async () =>
            {
                var response = await _client.SearchFacilityLocationLocalCodeMappingsAsync(
                    new SearchFacilityLocationLocalCodeMappingsRequestApiModel
                    {
                        FacilityId = facilityId,
                        PageSize = 10,
                        PageNumber = 1
                    },
                    ct);
                if (response.IsSuccessStatusCode && response.Body?.Records is { Count: > 0 })
                {
                    throw new InvalidOperationException("Local-code mappings remain after facility cleanup.");
                }

                return response;
            }, ct: ct));

            // POST → 201
            results.Add(await RunStepAsync(StepNames.Post201, 201, async () =>
            {
                var request = new CreateNormalizationOperationRequestApiModel
                {
                    ResourceTypes = ["Location"],
                    FacilityId = facilityId,
                    Operation = new CreateNormalizationOperationDetailsApiModel
                    {
                        OperationType = "CopyProperty",
                        Name = "ApiHealth Test Operation",
                        Description = "Api Health stability test",
                        SourceFhirPath = "identifier.value",
                        TargetFhirPath = "type[0].coding.code"
                    },
                    Description = "ApiHealth test — CopyProperty",
                    VendorVersionIds = []
                };
                var resp = await _client.CreateOperationAsync(request, ct);
                if (resp.IsSuccessStatusCode) operationCreated = true;
                return resp;
            }, ct: ct));

            // POST → 400 (invalid operation type)
            results.Add(await RunStepAsync(StepNames.Post400InvalidOperationType, 400, async () =>
                await _client.CreateOperationAsync(new CreateNormalizationOperationRequestApiModel
                {
                    ResourceTypes = ["Location"],
                    FacilityId = facilityId,
                    Operation = new CreateNormalizationOperationDetailsApiModel
                    {
                        OperationType = "NotARealOperation",
                        Name = "ApiHealth Invalid Operation Type",
                        Description = "Api Health invalid test",
                        SourceFhirPath = "identifier.value",
                        TargetFhirPath = "type[0].coding.code"
                    },
                    Description = "ApiHealth invalid test",
                    VendorVersionIds = []
                }, ct), ct: ct));

            // POST → 400 (empty resourceTypes)
            results.Add(await RunStepAsync(StepNames.Post400EmptyResourceTypes, 400, async () =>
                await _client.CreateOperationAsync(new CreateNormalizationOperationRequestApiModel
                {
                    ResourceTypes = [],
                    FacilityId = facilityId,
                    Operation = new CreateNormalizationOperationDetailsApiModel
                    {
                        OperationType = "CopyProperty",
                        Name = "ApiHealth Invalid Operation",
                        Description = "Api Health invalid test",
                        SourceFhirPath = "identifier.value",
                        TargetFhirPath = "type[0].coding.code"
                    },
                    Description = "ApiHealth invalid test",
                    VendorVersionIds = []
                }, ct), ct: ct));

            // GET → 200 (has results)
            results.Add(await RunStepAsync(StepNames.Get200HasResults, 200, async () =>
            {
                var resp = await _client.SearchFacilityOperationsAsync(facilityId, cancellationToken: ct);
                if (resp.IsSuccessStatusCode && (resp.Body?.Records == null || resp.Body.Records.Count == 0))
                    throw new InvalidOperationException("Expected at least one operation after create.");
                return resp;
            }, ct: ct));

            // GET → 200 (sequences)
            results.Add(await RunStepAsync(StepNames.Get200Sequences, 200, async () =>
                await _client.GetOperationSequencesAsync(facilityId, ct), ct: ct));

            // SEQUENCES POST → 201
            results.Add(await RunStepAsync(StepNames.SequencesPost201, 201, async () =>
            {
                var search = await _client.SearchFacilityOperationsAsync(facilityId, cancellationToken: ct);
                var operationId = search.Body?.Records?.FirstOrDefault()?.Id;
                if (operationId == null || operationId == Guid.Empty)
                    throw new InvalidOperationException("Expected at least one operation ID for sequence creation.");

                return await _client.CreateOperationSequencesAsync(
                    facilityId,
                    "Location",
                    [new CreateNormalizationOperationSequenceApiModel { OperationId = operationId, Sequence = 1 }],
                    ct);
            }, ct: ct));

            // SEQUENCES POST → 400 (empty facility)
            results.Add(await RunStepAsync(StepNames.SequencesPost400EmptyFacility, 400, async () =>
                await _client.CreateOperationSequencesAsync(
                    " ",
                    "Location",
                    [new CreateNormalizationOperationSequenceApiModel { OperationId = Guid.NewGuid(), Sequence = 1 }],
                    ct), ct: ct));

            // SEQUENCES POST → 400 (empty body)
            results.Add(await RunStepAsync(StepNames.SequencesPost400EmptyBody, 400, async () =>
                await _client.CreateOperationSequencesAsync(facilityId, "Location", [], ct), ct: ct));

            // GET → 400 (bad facility)
            results.Add(await RunStepAsync(StepNames.Get400BadFacility, 400, async () =>
                await _client.SearchFacilityOperationsAsync(" ", cancellationToken: ct), ct: ct));

            // GET → 400 (sequences bad facility)
            results.Add(await RunStepAsync(StepNames.Get400SequencesBadFacility, 400, async () =>
                await _client.GetOperationSequencesAsync(" ", ct), ct: ct));

            // DELETE → 404 (no records)
            var ghostFacilityId = $"ApiHealth-Norm-Ghost-{Guid.NewGuid():N}";
            results.Add(await RunStepAsync(StepNames.Delete404NoRecords, 404, async () =>
                await _client.DeleteFacilityOperationsAsync(ghostFacilityId, ct), ct: ct));

            // SEQUENCES DELETE → 400 (empty facility)
            results.Add(await RunStepAsync(StepNames.SequencesDelete400EmptyFacility, 400, async () =>
                await _client.DeleteOperationSequencesAsync(" ", "Location", ct), ct: ct));

            // SEQUENCES DELETE → 404
            results.Add(await RunStepAsync(StepNames.SequencesDelete404, 404, async () =>
                await _client.DeleteOperationSequencesAsync(facilityId, "Observation", ct), ct: ct));

            // SEQUENCES DELETE → 204
            results.Add(await RunStepAsync(StepNames.SequencesDelete204, 204, async () =>
                await _client.DeleteOperationSequencesAsync(facilityId, "Location", ct), ct: ct));

            // DELETE → 204
            results.Add(await RunStepAsync(StepNames.Delete204, 204, async () =>
            {
                var existing = await _client.SearchFacilityOperationsAsync(facilityId, cancellationToken: ct);
                if (existing.IsSuccessStatusCode && (existing.Body?.Records == null || existing.Body.Records.Count == 0))
                {
                    await _client.CreateOperationAsync(new CreateNormalizationOperationRequestApiModel
                    {
                        ResourceTypes = ["Location"],
                        FacilityId = facilityId,
                        Operation = new CreateNormalizationOperationDetailsApiModel
                        {
                            OperationType = "CopyProperty",
                            Name = "ApiHealth Recovery Operation",
                            Description = "Ensures DELETE → 204 has data to remove",
                            SourceFhirPath = "identifier.value",
                            TargetFhirPath = "type[0].coding.code"
                        },
                        Description = "ApiHealth recovery operation",
                        VendorVersionIds = []
                    }, ct);
                }

                var resp = await _client.DeleteFacilityOperationsAsync(facilityId, ct);
                if (resp.IsSuccessStatusCode) operationCreated = false;
                return resp;
            }, ct: ct));

            // GET → 200 (empty)
            results.Add(await RunStepAsync(StepNames.Get200Empty, 200, async () =>
            {
                var resp = await _client.SearchFacilityOperationsAsync(facilityId, cancellationToken: ct);
                if (resp.IsSuccessStatusCode && resp.Body?.Records is { Count: > 0 })
                    throw new InvalidOperationException("Operations still exist after deletion.");
                return resp;
            }, ct: ct));


        }
        finally
        {
            if (operationCreated) await TryCleanupAsync(() => _client.DeleteFacilityOperationsAsync(facilityId, ct));
            if (facilityLocationCreated) await TryCleanupAsync(() => _client.DeleteFacilityLocationLocalCodeMappingsForFacilityAsync(facilityId, ct));
            if (facilityCreated) await TryCleanupAsync(() => _facilityClient.DeleteAsync(facilityId, ct));
        }

        return results;
    }

    private async Task CreateFacilityAsync(string facilityId, CancellationToken ct)
    {
        var model = new FacilityModel
        {
            FacilityId = facilityId,
            FacilityName = facilityId,
            TimeZone = "America/Chicago",
            Vendor = new VendorModel
            {
                Name = "Epic"
            },
            ScheduledReports = new TenantScheduledReportConfig { Daily = [], Weekly = [], Monthly = [] }
        };
        await _facilityClient.CreateAsync(model, ct);
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
