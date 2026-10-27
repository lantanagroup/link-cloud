using System.Text.Json;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Normalization;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Exceptions;
using LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using EncounterMapping = LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Encounter.EncounterMapping;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Encounter;

public sealed class EncounterMappingService : IEncounterMappingService
{
    private const string ServiceName = "Normalization";
    private const string OperationTypeCodeMap = "CodeMap";
    private const string ResourceTypeEncounter = "Encounter";
    private const string EncounterTypeFhirPath = "type";
    private const string OperationName = "NHSN Encounter Type Code Map";

    private const int SearchPageSize = 100;
    private const char TargetSeparator = '|';

    private static readonly JsonSerializerOptions OperationJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly INormalizationServiceClient _normalizationClient;
    private readonly INormalizationRawClient _normalizationRawClient;
    private readonly INhsnUserContext _userContext;

    public EncounterMappingService(
        INormalizationServiceClient normalizationClient,
        INormalizationRawClient normalizationRawClient,
        INhsnUserContext userContext)
    {
        _normalizationClient = normalizationClient;
        _normalizationRawClient = normalizationRawClient;
        _userContext = userContext;
    }

    public async Task<IReadOnlyList<EncounterMapping>> GetAsync(CancellationToken cancellationToken = default)
    {
        var facilityId = _userContext.RequireFacilityId();

        var existing = await FindEncounterCodeMapOperationAsync(facilityId, cancellationToken);
        if (existing is null)
        {
            return [];
        }

        var operationJson = DeserializeOperationJson(existing.OperationJson);
        return FlattenToMappings(operationJson);
    }

    public async Task SaveAsync(IReadOnlyList<EncounterMapping> mappings, CancellationToken cancellationToken = default)
    {
        var facilityId = _userContext.RequireFacilityId();

        var codeSystemMaps = BuildCodeSystemMaps(mappings);
        var operationDetails = new CreateNormalizationOperationDetailsApiModel
        {
            OperationType = OperationTypeCodeMap,
            Name = OperationName,
            Description = "Maps this facility's local Encounter.type codes to CPT/SNOMED reference codes for NHSN reporting.",
            FhirPath = EncounterTypeFhirPath,
            CodeSystemMaps = codeSystemMaps
        };

        var existing = await FindEncounterCodeMapOperationAsync(facilityId, cancellationToken);

        if (existing is null)
        {
            var createResponse = await _normalizationClient.CreateOperationAsync(new CreateNormalizationOperationRequestApiModel
            {
                ResourceTypes = [ResourceTypeEncounter],
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
            ResourceTypes = [ResourceTypeEncounter],
            FacilityId = facilityId,
            Operation = operationDetails,
            IsDisabled = false,
            VendorVersionIds = []
        }, cancellationToken);
    }

    private async Task<NormalizationOperationApiModel?> FindEncounterCodeMapOperationAsync(string facilityId, CancellationToken cancellationToken)
    {
        for (var pageNumber = 1; ; pageNumber++)
        {
            var response = await _normalizationClient.SearchFacilityOperationsAsync(
                facilityId, includeDisabled: true, pageSize: SearchPageSize, pageNumber: pageNumber,
                cancellationToken: cancellationToken);
            var page = LinkResponseHandler.Require(response, ServiceName, nameof(FindEncounterCodeMapOperationAsync));

            var match = page.Records.FirstOrDefault(IsEncounterTypeCodeMap);
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

    private static bool IsEncounterTypeCodeMap(NormalizationOperationApiModel operation) =>
        string.Equals(operation.OperationType, OperationTypeCodeMap, StringComparison.OrdinalIgnoreCase)
        && string.Equals(operation.Name, OperationName, StringComparison.OrdinalIgnoreCase)
        && operation.OperationResourceTypes.Any(rt =>
            string.Equals(rt.Resource?.ResourceName, ResourceTypeEncounter, StringComparison.OrdinalIgnoreCase));

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

    private static List<EncounterMapping> FlattenToMappings(CodeMapOperationJson operation)
    {
        var mappings = new List<EncounterMapping>();

        foreach (var codeSystemMap in operation.CodeSystemMaps)
        {
            foreach (var (localCode, entry) in codeSystemMap.CodeMaps)
            {
                mappings.Add(new EncounterMapping
                {
                    System = codeSystemMap.SourceSystem,
                    Code = localCode,
                    Display = string.IsNullOrEmpty(entry.Display) ? null : entry.Display,
                    EncounterType = $"{codeSystemMap.TargetSystem}{TargetSeparator}{entry.Code}"
                });
            }
        }

        return mappings;
    }

    private static List<CreateNormalizationCodeSystemMapApiModel> BuildCodeSystemMaps(IReadOnlyList<EncounterMapping> mappings)
    {
        var groups = new Dictionary<(string SourceSystem, string TargetSystem), Dictionary<string, CreateNormalizationCodeMapEntryApiModel>>();

        foreach (var mapping in mappings)
        {
            if (string.IsNullOrWhiteSpace(mapping.System) || string.IsNullOrWhiteSpace(mapping.Code))
            {
                continue;
            }

            var separatorIndex = mapping.EncounterType.IndexOf(TargetSeparator);
            if (separatorIndex <= 0 || separatorIndex == mapping.EncounterType.Length - 1)
            {
                continue;
            }

            var targetSystem = mapping.EncounterType[..separatorIndex];
            var targetCode = mapping.EncounterType[(separatorIndex + 1)..];
            var display = string.IsNullOrWhiteSpace(mapping.Display) ? targetCode : mapping.Display;

            var key = (mapping.System, targetSystem);
            if (!groups.TryGetValue(key, out var codeMaps))
            {
                codeMaps = new Dictionary<string, CreateNormalizationCodeMapEntryApiModel>();
                groups[key] = codeMaps;
            }

            codeMaps[mapping.Code] = new CreateNormalizationCodeMapEntryApiModel
            {
                Code = targetCode,
                Display = display
            };
        }

        return groups
            .Select(group => new CreateNormalizationCodeSystemMapApiModel
            {
                SourceSystem = group.Key.SourceSystem,
                TargetSystem = group.Key.TargetSystem,
                CodeMaps = group.Value
            })
            .ToList();
    }
}
