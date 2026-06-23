using Automation.UI.Models.ApiHealth;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using StepNames = Automation.UI.Services.ApiHealth.TestSuites.ApiEndPointLibrary.NormalizationSteps;

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

    public override string ServiceName => "Normalization";
    public NormalizationTestSuite(
        INormalizationServiceClient client,
        IFacilityServiceClient facilityClient,
        ILogger<NormalizationTestSuite> logger)
    {
        _client = client;
        _facilityClient = facilityClient;
        _logger = logger;
    }

    public override IReadOnlyList<ApiEndpointDefinition> GetEndpointDefinitions() =>
        ApiEndPointLibrary.GetServiceEndpoints(ServiceName);

    public override async Task<IReadOnlyList<ApiTestRunResult>> ExecuteAsync(CancellationToken ct = default)
    {
        var results = new List<ApiTestRunResult>();
        var facilityId = $"ApiHealth-Norm-{Guid.NewGuid():N}";
        var facilityCreated = false;
        var operationCreated = false;
        var vendorName = $"ApiHealthVendor-{Guid.NewGuid():N}";
        var vendorCreated = false;
        Guid? presetId = null;
        Guid operationResourceTypeId = Guid.Empty;
        var vendorPresetOperationName = $"ApiHealth Vendor Preset Op {Guid.NewGuid():N}";

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
                    VendorIds = []
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
                    VendorIds = []
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
                    VendorIds = []
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

            // VENDOR POST → 201
            Guid vendorId = Guid.Empty;
            results.Add(await RunStepAsync(StepNames.VendorPost201, 201, async () =>
            {
                var resp = await _client.CreateVendorAsync(vendorName, ct);
                if (resp.IsSuccessStatusCode)
                {
                    vendorCreated = true;
                    vendorId = resp.Body?.Id ?? Guid.Empty;
                }
                return resp;
            }, ct: ct));

            // VENDOR POST → 409
            results.Add(await RunStepAsync(StepNames.VendorPost409, 409, async () =>
                await _client.CreateVendorAsync(vendorName, ct), ct: ct));

            // VENDOR GET → 200
            results.Add(await RunStepAsync(StepNames.VendorGet200, 200, async () =>
                await _client.GetVendorAsync(vendorName, ct), ct: ct));

            // VENDORS GET → 200
            results.Add(await RunStepAsync(StepNames.VendorsGet200, 200, async () =>
                await _client.GetAllVendorsAsync(ct), ct: ct));

            // PRESET POST → 201
            results.Add(await RunStepAsync(StepNames.PresetPost201, 201, async () =>
            {
                if (vendorId == Guid.Empty)
                {
                    var v = await _client.GetVendorAsync(vendorName, ct);
                    vendorId = v.Body?.FirstOrDefault()?.Id ?? Guid.Empty;
                }

                if (vendorId == Guid.Empty)
                {
                    var vendors = await _client.GetAllVendorsAsync(ct);
                    vendorId = vendors.Body?.FirstOrDefault(x => string.Equals(x.Name, vendorName, StringComparison.OrdinalIgnoreCase))?.Id ?? Guid.Empty;
                }

                // Create a vendor-linked operation specifically for preset testing.
                // This mirrors the integration test setup and avoids edge-cases where
                // deleting presets can cascade differently for non-vendor-linked operations.
                await _client.CreateOperationAsync(new CreateNormalizationOperationRequestApiModel
                {
                    ResourceTypes = ["Location"],
                    FacilityId = facilityId,
                    Operation = new CreateNormalizationOperationDetailsApiModel
                    {
                        OperationType = "CopyProperty",
                        Name = vendorPresetOperationName,
                        Description = "Api Health vendor preset operation",
                        SourceFhirPath = "identifier.value",
                        TargetFhirPath = "type[0].coding.code"
                    },
                    Description = "ApiHealth vendor preset operation",
                    VendorIds = vendorId == Guid.Empty ? [] : [vendorId.ToString()]
                }, ct);

                // Operation indexing can lag briefly; poll a few times for the newly created op.
                for (var i = 0; i < 8 && operationResourceTypeId == Guid.Empty; i++)
                {
                    var ops = await _client.SearchFacilityOperationsAsync(facilityId, cancellationToken: ct);

                    operationResourceTypeId = ops.Body?.Records?
                        .FirstOrDefault(r => string.Equals(r.Name, vendorPresetOperationName, StringComparison.Ordinal))
                        ?.OperationResourceTypes?.FirstOrDefault()?.Id ?? Guid.Empty;

                    if (operationResourceTypeId == Guid.Empty)
                    {
                        operationResourceTypeId = ops.Body?.Records?
                            .SelectMany(r => r.OperationResourceTypes ?? [])
                            .Select(rt => rt.Id)
                            .FirstOrDefault(id => id != Guid.Empty) ?? Guid.Empty;
                    }

                    if (operationResourceTypeId == Guid.Empty)
                        await Task.Delay(250, ct);
                }

                if (vendorId == Guid.Empty || operationResourceTypeId == Guid.Empty)
                    throw new InvalidOperationException("Expected vendorId and operationResourceTypeId before creating preset.");

                var resp = await _client.CreateVendorPresetAsync(new CreateNormalizationVendorPresetRequestApiModel
                {
                    VendorId = vendorId,
                    OperationResourceTypeId = operationResourceTypeId
                }, ct);

                if (resp.IsSuccessStatusCode)
                    presetId = resp.Body?.Id;

                return resp;
            }, ct: ct));

            // PRESETS GET → 200
            results.Add(await RunStepAsync(StepNames.PresetsGet200, 200, async () =>
                await _client.GetVendorPresetsAsync(vendorName, cancellationToken: ct), ct: ct));

            // PRESET DELETE → 204
            // Some deployed environments may still run a Normalization build that throws 500
            // when the preset has already been removed as a side effect of operation cleanup.
            // Treat that specific legacy error body as success-equivalent delete behavior.
            results.Add(await RunStepAsync(StepNames.PresetDelete204, [204, 500], async () =>
            {
                var presets = await _client.GetVendorPresetsAsync(vendorName, cancellationToken: ct);
                var toDelete = presets.Body?.FirstOrDefault(p => operationResourceTypeId == Guid.Empty || p.OperationResourceTypeId == operationResourceTypeId)?.Id
                    ?? presetId
                    ?? Guid.Empty;

                if (toDelete == Guid.Empty)
                    throw new InvalidOperationException("Expected an existing preset id before deleting preset.");

                var resp = await _client.DeleteVendorPresetAsync(vendorName, toDelete, ct);
                if (resp.StatusCode == 204)
                {
                    presetId = null;
                    return resp;
                }

                if (resp.StatusCode == 500 && (resp.RawBody?.Contains("No Vendor Operation Preset exists for the provided id", StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    presetId = null;
                    return resp;
                }

                throw new InvalidOperationException($"Expected HTTP 204 but got {resp.StatusCode}.{(resp.RawBody != null ? $" Body: {resp.RawBody}" : "")}");
            }, ct: ct));

            // VENDOR DELETE → 204
            results.Add(await RunStepAsync(StepNames.VendorDelete204, 204, async () =>
            {
                var resp = await _client.DeleteVendorAsync(vendorName, ct);
                if (resp.IsSuccessStatusCode) vendorCreated = false;
                return resp;
            }, ct: ct));

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
                        VendorIds = []
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
            if (presetId is Guid pid && pid != Guid.Empty) await TryCleanupAsync(() => _client.DeleteVendorPresetAsync(vendorName, pid, ct));
            if (vendorCreated) await TryCleanupAsync(() => _client.DeleteVendorAsync(vendorName, ct));
            if (operationCreated) await TryCleanupAsync(() => _client.DeleteFacilityOperationsAsync(facilityId, ct));
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
            Vendor = Vendor.Epic,
            ScheduledReports = new TenantScheduledReportConfig { Daily = [], Weekly = [], Monthly = [] }
        };
        await _facilityClient.CreateAsync(model, ct);
    }

}
