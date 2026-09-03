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
/// Covers operations/sequences plus the LEGLINK-677 facility-location and HSLOC mapping APIs,
/// including SDK-reachable 4xx/409 paths and persisted LocationName/LocationAlias/LocalCodeSystem fields.
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
        var facilityCreated = false;
        var facilityLocationCreated = false;
        var operationCreated = false;
        var mappingsCreated = false;
        var locationId = $"ApiHealth-Loc-{Guid.NewGuid():N}";
        const string locationName = "ApiHealth Main";
        const string locationAlias = "apihealth-main";
        const string localCodeSystem = "http://example.org/apihealth-location";
        var localCode = $"code-{Guid.NewGuid():N}";
        var secondLocalCode = $"code-{Guid.NewGuid():N}";
        string? mappingId = null;
        string? secondMappingId = null;

        try
        {
            // Prerequisite: create facility (infrastructure, not tracked)
            await CreateFacilityAsync(facilityId, ct);
            facilityCreated = true;

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

            results.Add(await RunStepAsync(StepNames.LocationPost400EmptyLocationId, 400, () =>
                _client.CreateFacilityLocationAsync(facilityId, new CreateFacilityLocationRequestApiModel(), ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.LocationGet400EmptyLocationId, 400, () =>
                _client.GetFacilityLocationAsync(facilityId, " ", ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.LocationGet404, 404, () =>
                _client.GetFacilityLocationAsync(facilityId, $"missing-{Guid.NewGuid():N}", ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.LocationPost201, 201, async () =>
            {
                var resp = await _client.CreateFacilityLocationAsync(facilityId, new CreateFacilityLocationRequestApiModel
                {
                    LocationId = locationId,
                    LocationName = locationName,
                    LocationAlias = locationAlias
                }, ct);
                if (resp.IsSuccessStatusCode)
                    AssertFacilityLocation(resp.Body, facilityId, locationId, locationName, locationAlias);
                return resp;
            }, ct: ct));

            results.Add(await RunStepAsync(StepNames.LocationPost409Duplicate, 409, () =>
                _client.CreateFacilityLocationAsync(facilityId, new CreateFacilityLocationRequestApiModel
                {
                    LocationId = locationId,
                    LocationName = locationName,
                    LocationAlias = locationAlias
                }, ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.LocationGet200, 200, async () =>
            {
                var resp = await _client.GetFacilityLocationAsync(facilityId, locationId, ct);
                if (resp.IsSuccessStatusCode)
                    AssertFacilityLocation(resp.Body, facilityId, locationId, locationName, locationAlias);
                return resp;
            }, ct: ct));

            results.Add(await RunStepAsync(StepNames.MappingPost400EmptyLocalCode, 400, () =>
                _client.CreateFacilityLocationLocalCodeMappingAsync(facilityId, new CreateFacilityLocationLocalCodeMappingRequestApiModel
                {
                    LocationId = locationId,
                    LocalCodeSystem = localCodeSystem,
                    LocalCode = " "
                }, ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.MappingPost404UnknownLocation, 404, () =>
                _client.CreateFacilityLocationLocalCodeMappingAsync(facilityId, new CreateFacilityLocationLocalCodeMappingRequestApiModel
                {
                    LocationId = $"missing-{Guid.NewGuid():N}",
                    LocalCodeSystem = localCodeSystem,
                    LocalCode = localCode
                }, ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.MappingGet400EmptyId, 400, () =>
                _client.GetFacilityLocationLocalCodeMappingAsync(" ", ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.MappingGet404, 404, () =>
                _client.GetFacilityLocationLocalCodeMappingAsync(Guid.NewGuid().ToString("N"), ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.MappingPost201, 201, async () =>
            {
                var resp = await _client.CreateFacilityLocationLocalCodeMappingAsync(facilityId, new CreateFacilityLocationLocalCodeMappingRequestApiModel
                {
                    LocationId = locationId,
                    LocalCodeSystem = localCodeSystem,
                    LocalCode = localCode
                }, ct);
                if (resp.IsSuccessStatusCode)
                {
                    mappingId = resp.Body?.Id;
                    mappingsCreated = true;
                    AssertMapping(resp.Body, facilityId, locationId, locationName, locationAlias, localCodeSystem, localCode);
                }
                return resp;
            }, ct: ct));

            results.Add(await RunStepAsync(StepNames.MappingPost409Duplicate, 409, () =>
                _client.CreateFacilityLocationLocalCodeMappingAsync(facilityId, new CreateFacilityLocationLocalCodeMappingRequestApiModel
                {
                    LocationId = locationId,
                    LocalCodeSystem = localCodeSystem,
                    LocalCode = localCode
                }, ct), ct: ct));

            results.Add(mappingId is null
                ? SkipStepAsync(StepNames.MappingGet200, "MAPPING POST → 201 did not return a mapping id.")
                : await RunStepAsync(StepNames.MappingGet200, 200, async () =>
                {
                    var resp = await _client.GetFacilityLocationLocalCodeMappingAsync(mappingId, ct);
                    if (resp.IsSuccessStatusCode)
                        AssertMapping(resp.Body, facilityId, locationId, locationName, locationAlias, localCodeSystem, localCode);
                    return resp;
                }, ct: ct));

            results.Add(await RunStepAsync(StepNames.MappingSearch200HasResults, 200, async () =>
            {
                var resp = await _client.SearchFacilityLocationLocalCodeMappingsAsync(new SearchFacilityLocationLocalCodeMappingsRequestApiModel
                {
                    FacilityId = facilityId,
                    LocationId = locationId,
                    PageSize = 10,
                    PageNumber = 1
                }, ct);
                if (resp.IsSuccessStatusCode)
                {
                    var match = resp.Body?.Records?.FirstOrDefault(record => record.Id == mappingId)
                        ?? throw new InvalidOperationException("Expected the created HSLOC mapping in search results.");
                    AssertMapping(match, facilityId, locationId, locationName, locationAlias, localCodeSystem, localCode);
                }
                return resp;
            }, ct: ct));

            results.Add(await RunStepAsync(StepNames.MappingSearch200Empty, 200, async () =>
            {
                var resp = await _client.SearchFacilityLocationLocalCodeMappingsAsync(new SearchFacilityLocationLocalCodeMappingsRequestApiModel
                {
                    FacilityId = $"ApiHealth-Norm-Ghost-{Guid.NewGuid():N}",
                    PageSize = 10,
                    PageNumber = 1
                }, ct);
                if (resp.IsSuccessStatusCode && resp.Body?.Records is { Count: > 0 })
                    throw new InvalidOperationException("Expected no HSLOC mappings for an unused facility.");
                return resp;
            }, ct: ct));

            results.Add(mappingId is null
                ? SkipStepAsync(StepNames.MappingPut400EmptyLocalCode, "MAPPING POST → 201 did not return a mapping id.")
                : await RunStepAsync(StepNames.MappingPut400EmptyLocalCode, 400, () =>
                    _client.UpdateFacilityLocationLocalCodeMappingAsync(mappingId, new UpdateFacilityLocationLocalCodeMappingRequestApiModel
                    {
                        LocalCodeSystem = localCodeSystem,
                        LocalCode = " "
                    }, ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.MappingPut404, 404, () =>
                _client.UpdateFacilityLocationLocalCodeMappingAsync(Guid.NewGuid().ToString("N"), new UpdateFacilityLocationLocalCodeMappingRequestApiModel
                {
                    LocalCodeSystem = localCodeSystem,
                    LocalCode = localCode
                }, ct), ct: ct));

            results.Add(mappingId is null
                ? SkipStepAsync(StepNames.MappingPut202, "MAPPING POST → 201 did not return a mapping id.")
                : await RunStepAsync(StepNames.MappingPut202, 202, async () =>
                {
                    var updatedCode = $"{localCode}-upd";
                    var resp = await _client.UpdateFacilityLocationLocalCodeMappingAsync(mappingId, new UpdateFacilityLocationLocalCodeMappingRequestApiModel
                    {
                        LocalCodeSystem = localCodeSystem,
                        LocalCode = updatedCode
                    }, ct);
                    if (resp.IsSuccessStatusCode)
                    {
                        localCode = updatedCode;
                        AssertMapping(resp.Body, facilityId, locationId, locationName, locationAlias, localCodeSystem, updatedCode);
                    }
                    return resp;
                }, ct: ct));

            results.Add(mappingId is null
                ? SkipStepAsync(StepNames.MappingPut409Duplicate, "MAPPING POST → 201 did not return a mapping id.")
                : await RunStepAsync(StepNames.MappingPut409Duplicate, 409, async () =>
                {
                    var second = await _client.CreateFacilityLocationLocalCodeMappingAsync(facilityId, new CreateFacilityLocationLocalCodeMappingRequestApiModel
                    {
                        LocationId = locationId,
                        LocalCodeSystem = localCodeSystem,
                        LocalCode = secondLocalCode
                    }, ct);
                    if (!second.IsSuccessStatusCode || string.IsNullOrWhiteSpace(second.Body?.Id))
                        throw new InvalidOperationException($"Could not create a second mapping to prove PUT 409. HTTP {second.StatusCode}: {second.RawBody}");

                    secondMappingId = second.Body.Id;
                    return await _client.UpdateFacilityLocationLocalCodeMappingAsync(secondMappingId, new UpdateFacilityLocationLocalCodeMappingRequestApiModel
                    {
                        LocalCodeSystem = localCodeSystem,
                        LocalCode = localCode
                    }, ct);
                }, ct: ct));

            results.Add(await RunStepAsync(StepNames.MappingDelete400EmptyId, 400, () =>
                _client.DeleteFacilityLocationLocalCodeMappingAsync(" ", ct), ct: ct));

            results.Add(secondMappingId is null
                ? SkipStepAsync(StepNames.MappingDelete204, "Second mapping was not created, so DELETE by id has nothing to remove.")
                : await RunStepAsync(StepNames.MappingDelete204, 204, async () =>
                {
                    var resp = await _client.DeleteFacilityLocationLocalCodeMappingAsync(secondMappingId, ct);
                    if (resp.IsSuccessStatusCode)
                        secondMappingId = null;
                    return resp;
                }, ct: ct));

            results.Add(await RunStepAsync(StepNames.MappingDeleteFacility400EmptyFacility, 400, () =>
                _client.DeleteFacilityLocationLocalCodeMappingsForFacilityAsync(" ", ct), ct: ct));

            results.Add(await RunStepAsync(StepNames.MappingDeleteFacility204, 204, async () =>
            {
                var resp = await _client.DeleteFacilityLocationLocalCodeMappingsForFacilityAsync(facilityId, ct);
                if (resp.IsSuccessStatusCode)
                    mappingsCreated = false;
                return resp;
            }, ct: ct));
        }
        finally
        {
            if (secondMappingId != null)
                await TryCleanupAsync(() => _client.DeleteFacilityLocationLocalCodeMappingAsync(secondMappingId, ct));
            if (mappingsCreated)
                await TryCleanupAsync(() => _client.DeleteFacilityLocationLocalCodeMappingsForFacilityAsync(facilityId, ct));
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

    private static void AssertFacilityLocation(
        FacilityLocationApiModel? location,
        string facilityId,
        string locationId,
        string locationName,
        string locationAlias)
    {
        if (location is null)
            throw new InvalidOperationException("Expected a facility location in the response body.");
        if (!string.Equals(location.FacilityId, facilityId, StringComparison.Ordinal)
            || !string.Equals(location.LocationId, locationId, StringComparison.Ordinal)
            || !string.Equals(location.LocationName, locationName, StringComparison.Ordinal)
            || !string.Equals(location.LocationAlias, locationAlias, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Facility location fields were not persisted. FacilityId={location.FacilityId}, LocationId={location.LocationId}, LocationName={location.LocationName}, LocationAlias={location.LocationAlias}.");
        }
    }

    private static void AssertMapping(
        FacilityLocationLocalCodeMappingApiModel? mapping,
        string facilityId,
        string locationId,
        string locationName,
        string locationAlias,
        string localCodeSystem,
        string localCode)
    {
        if (mapping is null)
            throw new InvalidOperationException("Expected an HSLOC mapping in the response body.");
        if (!string.Equals(mapping.FacilityId, facilityId, StringComparison.Ordinal)
            || !string.Equals(mapping.LocationId, locationId, StringComparison.Ordinal)
            || !string.Equals(mapping.LocationName, locationName, StringComparison.Ordinal)
            || !string.Equals(mapping.LocationAlias, locationAlias, StringComparison.Ordinal)
            || !string.Equals(mapping.LocalCodeSystem, localCodeSystem, StringComparison.Ordinal)
            || !string.Equals(mapping.LocalCode, localCode, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"HSLOC mapping fields were not persisted. FacilityId={mapping.FacilityId}, LocationId={mapping.LocationId}, LocationName={mapping.LocationName}, LocationAlias={mapping.LocationAlias}, LocalCodeSystem={mapping.LocalCodeSystem}, LocalCode={mapping.LocalCode}.");
        }
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
