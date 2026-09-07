using System.Text.Json;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Hsloc;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Normalization;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Exceptions;
using LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Hsloc;

// Mirrors EncounterMappingService's Normalization CodeMap wiring for the HSLOC Location
// Identification step, mapping the facility's local Location values to the NHSN HSLOC vocabulary.
//
// Two constants below are inferred, not confirmed against a real spec, and should be reviewed by
// whoever owns this LEGLINK story before this goes live against a real Normalization instance:
//   - LocationFhirPath: EncounterMappingService's equivalent ("type") is fixed regardless of
//     vendor because vendor differences live in the code system, not the FHIR element. The one
//     signal for HSLOC is MockApiClient's hslocSourceLabel, which calls out
//     "Location.identifier value" for the vendor profile that doesn't have its own vendor-specific
//     label — hence "identifier" here. If HSLOC mappings are meant to key off Location.type (or
//     something else) instead, change LocationFhirPath only.
//   - TargetSystem: no HSLOC codesystem URI is referenced elsewhere in this codebase, so this uses
//     the short token "HSLOC" for consistency with EncounterCodeCatalog's "CPT"/"SNOMED" style
//     rather than inventing a URI that might not match what NHSN submission actually expects.
public sealed class HslocMappingService : IHslocMappingService
{
    private const string ServiceName = "Normalization";
    private const string OperationTypeCodeMap = "CodeMap";
    private const string ResourceTypeLocation = "Location";
    private const string LocationFhirPath = "identifier";
    private const string OperationName = "NHSN HSLOC Location Code Map";

    // Every row maps the same facility's single local-code space, so — unlike Encounter, which
    // keys mappings by whatever source system each row declares — there is exactly one
    // CodeSystemMap group and no per-row source system to round-trip.
    private const string LocalSourceSystem = "Location";
    private const string TargetSystem = "HSLOC";

    private const int SearchPageSize = 100;

    private static readonly JsonSerializerOptions OperationJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly INormalizationServiceClient _normalizationClient;
    private readonly INormalizationRawClient _normalizationRawClient;
    private readonly INhsnUserContext _userContext;

    public HslocMappingService(
        INormalizationServiceClient normalizationClient,
        INormalizationRawClient normalizationRawClient,
        INhsnUserContext userContext)
    {
        _normalizationClient = normalizationClient;
        _normalizationRawClient = normalizationRawClient;
        _userContext = userContext;
    }

    public async Task<IReadOnlyList<HslocMapping>> GetAsync(CancellationToken cancellationToken = default)
    {
        var facilityId = _userContext.RequireFacilityId();

        var existing = await FindHslocCodeMapOperationAsync(facilityId, cancellationToken);
        if (existing is null)
        {
            return [];
        }

        var operationJson = DeserializeOperationJson(existing.OperationJson);
        return FlattenToMappings(operationJson);
    }

    public async Task SaveAsync(IReadOnlyList<HslocMapping> mappings, CancellationToken cancellationToken = default)
    {
        var facilityId = _userContext.RequireFacilityId();

        var codeSystemMaps = BuildCodeSystemMaps(mappings);
        var operationDetails = new CreateNormalizationOperationDetailsApiModel
        {
            OperationType = OperationTypeCodeMap,
            Name = OperationName,
            Description = "Maps this facility's local Location values to NHSN HSLOC codes for NHSN reporting.",
            FhirPath = LocationFhirPath,
            CodeSystemMaps = codeSystemMaps
        };

        var existing = await FindHslocCodeMapOperationAsync(facilityId, cancellationToken);

        if (existing is null)
        {
            var createResponse = await _normalizationClient.CreateOperationAsync(new CreateNormalizationOperationRequestApiModel
            {
                ResourceTypes = [ResourceTypeLocation],
                FacilityId = facilityId,
                Operation = operationDetails,
                Description = operationDetails.Description,
                VendorVersionIds = []
            }, cancellationToken);

            LinkResponseHandler.EnsureSuccess(createResponse, ServiceName, nameof(SaveAsync));
            return;
        }

        await _normalizationRawClient.UpdateOperationAsync(new UpdateNormalizationOperationRequestApiModel
        {
            Id = existing.Id,
            ResourceTypes = [ResourceTypeLocation],
            FacilityId = facilityId,
            Operation = operationDetails,
            IsDisabled = false,
            VendorVersionIds = []
        }, cancellationToken);
    }

    private async Task<NormalizationOperationApiModel?> FindHslocCodeMapOperationAsync(string facilityId, CancellationToken cancellationToken)
    {
        for (var pageNumber = 1; ; pageNumber++)
        {
            var response = await _normalizationClient.SearchFacilityOperationsAsync(
                facilityId, includeDisabled: true, pageSize: SearchPageSize, pageNumber: pageNumber,
                cancellationToken: cancellationToken);
            var page = LinkResponseHandler.Require(response, ServiceName, nameof(FindHslocCodeMapOperationAsync));

            var match = page.Records.FirstOrDefault(IsHslocCodeMap);
            if (match is not null)
            {
                return match;
            }

            if (page.Records.Count < SearchPageSize)
            {
                return null;
            }
        }
    }

    private static bool IsHslocCodeMap(NormalizationOperationApiModel operation) =>
        string.Equals(operation.OperationType, OperationTypeCodeMap, StringComparison.OrdinalIgnoreCase)
        && string.Equals(operation.Name, OperationName, StringComparison.OrdinalIgnoreCase)
        && operation.OperationResourceTypes.Any(rt =>
            string.Equals(rt.Resource?.ResourceName, ResourceTypeLocation, StringComparison.OrdinalIgnoreCase));

    private static CodeMapOperationJson DeserializeOperationJson(string operationJson)
    {
        if (string.IsNullOrWhiteSpace(operationJson))
        {
            return new CodeMapOperationJson();
        }

        try
        {
            return JsonSerializer.Deserialize<CodeMapOperationJson>(operationJson, OperationJsonOptions) ?? new CodeMapOperationJson();
        }
        catch (JsonException ex)
        {
            throw new LinkServiceException(ServiceName, nameof(GetAsync), 0, null, operationJson, null, ex);
        }
    }

    private static List<HslocMapping> FlattenToMappings(CodeMapOperationJson operation)
    {
        var mappings = new List<HslocMapping>();

        foreach (var codeSystemMap in operation.CodeSystemMaps)
        {
            foreach (var (localCode, entry) in codeSystemMap.CodeMaps)
            {
                mappings.Add(new HslocMapping
                {
                    SourceCode = localCode,
                    SourceDisplay = string.IsNullOrEmpty(entry.Display) ? null : entry.Display,
                    HslocCode = entry.Code
                });
            }
        }

        return mappings;
    }

    private static List<CreateNormalizationCodeSystemMapApiModel> BuildCodeSystemMaps(IReadOnlyList<HslocMapping> mappings)
    {
        var codeMaps = new Dictionary<string, CreateNormalizationCodeMapEntryApiModel>();

        foreach (var mapping in mappings)
        {
            if (string.IsNullOrWhiteSpace(mapping.SourceCode) || string.IsNullOrWhiteSpace(mapping.HslocCode))
            {
                continue;
            }

            codeMaps[mapping.SourceCode] = new CreateNormalizationCodeMapEntryApiModel
            {
                Code = mapping.HslocCode,
                Display = string.IsNullOrWhiteSpace(mapping.SourceDisplay) ? mapping.HslocCode : mapping.SourceDisplay
            };
        }

        if (codeMaps.Count == 0)
        {
            return [];
        }

        return
        [
            new CreateNormalizationCodeSystemMapApiModel
            {
                SourceSystem = LocalSourceSystem,
                TargetSystem = TargetSystem,
                CodeMaps = codeMaps
            }
        ];
    }
}
