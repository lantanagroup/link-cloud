using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Hsloc;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Normalization;
using LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Hsloc;

//   - normalization/hsloc-mappings/*  (search/get/create/update/delete a facility's mappings)
//   - normalization/HSLOC             (the HSLOC reference vocabulary, to resolve a code to its id)
// No generic CodeMap Operation, no static/hardcoded HSLOC data.
//
// One caveat: a hsloc-mappings row is keyed by FacilityLocationId, a foreign key into
// Normalization's separate FacilityLocation table (normalization/facility-locations/...) — a route
// with no "hsloc" in it. HslocMapping (and the UI) carry only a local code, not a location id, so
// this service treats each SourceCode as its own LocationId and provisions the FacilityLocation
// on demand (GetFacilityLocationAsync/CreateFacilityLocationAsync) the first time a given SourceCode
// is saved. it exists only to satisfy a foreign key, the HSLOC mapping table itself requires.
public sealed class HslocMappingService : IHslocMappingService
{
    private const string ServiceName = "Normalization";

    // Every row maps the same facility's single local-location-code space
    private const string LocalCodeSystem = "Location";

    private const int SearchPageSize = 100;

    private readonly INormalizationServiceClient _normalizationClient;
    private readonly INhsnUserContext _userContext;
    private readonly ILogger<HslocMappingService> _logger;

    public HslocMappingService(
        INormalizationServiceClient normalizationClient,
        INhsnUserContext userContext,
        ILogger<HslocMappingService> logger)
    {
        _normalizationClient = normalizationClient;
        _userContext = userContext;
        _logger = logger;
    }

    public async Task<IReadOnlyList<HslocMapping>> GetAsync(CancellationToken cancellationToken = default)
    {
        var facilityId = _userContext.RequireFacilityId();
        var rows = await SearchAllMappingsAsync(facilityId, cancellationToken);

        var mappings = new List<HslocMapping>(rows.Count);
        foreach (var row in rows)
        {
            // A row with no HSLOCCode is an unmapped local code (search can return those); a row
            // missing LocalCode would mean Normalization returned a mapping with no source code at
            // all. Either way, HslocMapping requires both — flag and drop rather than surface a
            // mapping with a blank required field to the UI.
            if (string.IsNullOrWhiteSpace(row.LocalCode) || string.IsNullOrWhiteSpace(row.HSLOCCode))
            {
                _logger.LogWarning(
                    "Skipping HSLOC mapping {MappingId} for facility {FacilityId}: missing required field(s) (LocalCode={LocalCode}, HSLOCCode={HSLOCCode}).",
                    row.Id, facilityId, row.LocalCode, row.HSLOCCode);
                continue;
            }

            mappings.Add(new HslocMapping
            {
                SourceCode = row.LocalCode,
                SourceDisplay = string.IsNullOrWhiteSpace(row.LocationName) ? row.LocationAlias : row.LocationName,
                HslocCode = row.HSLOCCode
            });
        }

        return mappings;
    }

    public async Task SaveAsync(IReadOnlyList<HslocMapping> mappings, CancellationToken cancellationToken = default)
    {
        var facilityId = _userContext.RequireFacilityId();

        var hslocIdByCode = await BuildHslocIdLookupAsync(cancellationToken);
        var existingByLocalCode = (await SearchAllMappingsAsync(facilityId, cancellationToken))
            .Where(row => !string.IsNullOrWhiteSpace(row.LocalCode))
            .GroupBy(row => row.LocalCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var seenLocalCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in mappings)
        {
            if (string.IsNullOrWhiteSpace(mapping.SourceCode) || string.IsNullOrWhiteSpace(mapping.HslocCode))
            {
                _logger.LogWarning(
                    "Skipping HSLOC mapping for facility {FacilityId}: SourceCode and HslocCode are both required.",
                    facilityId);
                continue;
            }

            if (!hslocIdByCode.TryGetValue(mapping.HslocCode, out var hslocId))
            {
                _logger.LogWarning(
                    "Skipping HSLOC mapping for facility {FacilityId}: HSLOC code {HslocCode} was not found in the reference vocabulary.",
                    facilityId, mapping.HslocCode);
                continue;
            }

            seenLocalCodes.Add(mapping.SourceCode);

            if (existingByLocalCode.TryGetValue(mapping.SourceCode, out var existingRow))
            {
                if (existingRow.HSLOCId == hslocId)
                {
                    continue;
                }

                var updateResponse = await _normalizationClient.UpdateFacilityLocationLocalCodeMappingAsync(
                    existingRow.Id,
                    new UpdateFacilityLocationLocalCodeMappingRequestApiModel
                    {
                        LocalCodeSystem = LocalCodeSystem,
                        LocalCode = mapping.SourceCode,
                        HSLOCId = hslocId
                    },
                    cancellationToken);
                LinkResponseHandler.Require(updateResponse, ServiceName, nameof(SaveAsync));
                continue;
            }

            await EnsureFacilityLocationAsync(facilityId, mapping, cancellationToken);

            var createResponse = await _normalizationClient.CreateFacilityLocationLocalCodeMappingAsync(
                facilityId,
                new CreateFacilityLocationLocalCodeMappingRequestApiModel
                {
                    LocationId = mapping.SourceCode,
                    LocalCodeSystem = LocalCodeSystem,
                    LocalCode = mapping.SourceCode,
                    HSLOCId = hslocId
                },
                cancellationToken);
            LinkResponseHandler.Require(createResponse, ServiceName, nameof(SaveAsync));
        }

        // Replace-the-whole-set semantics: anything not present in the incoming list is removed.
        foreach (var stale in existingByLocalCode.Values.Where(row => !seenLocalCodes.Contains(row.LocalCode)))
        {
            var deleteResponse = await _normalizationClient.DeleteFacilityLocationLocalCodeMappingAsync(stale.Id, cancellationToken);
            LinkResponseHandler.EnsureSuccess(deleteResponse, ServiceName, nameof(SaveAsync));
        }
    }

    private async Task<List<FacilityLocationLocalCodeMappingApiModel>> SearchAllMappingsAsync(
        string facilityId, CancellationToken cancellationToken)
    {
        var rows = new List<FacilityLocationLocalCodeMappingApiModel>();
        for (var pageNumber = 1; ; pageNumber++)
        {
            var response = await _normalizationClient.SearchFacilityLocationLocalCodeMappingsAsync(
                new SearchFacilityLocationLocalCodeMappingsRequestApiModel
                {
                    FacilityId = facilityId,
                    PageSize = SearchPageSize,
                    PageNumber = pageNumber
                }, cancellationToken);
            var page = LinkResponseHandler.Require(response, ServiceName, nameof(GetAsync));

            rows.AddRange(page.Records);
            if (page.Records.Count < SearchPageSize)
            {
                return rows;
            }
        }
    }

    // Resolves each HSLOC reference code to its id, since the hsloc-mappings create/update models
    // take HSLOCId (a Guid), not the code text — read via GetHslocCodesAsync
    private async Task<Dictionary<string, Guid>> BuildHslocIdLookupAsync(CancellationToken cancellationToken)
    {
        var response = await _normalizationClient.GetHslocCodesAsync(includeInactive: false, cancellationToken: cancellationToken);
        var codes = LinkResponseHandler.OptionalFromRawBody<List<HslocReferenceCodeJson>>(response, ServiceName, nameof(SaveAsync))
                    ?? [];

        var lookup = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in codes)
        {
            if (string.IsNullOrWhiteSpace(code.HSLOCCode))
            {
                _logger.LogWarning("Skipping HSLOC reference code {Id}: missing required HSLOCCode.", code.Id);
                continue;
            }

            lookup[code.HSLOCCode] = code.Id;
        }

        return lookup;
    }

    // Get-or-create the FacilityLocation a hsloc-mappings row is foreign-keyed to. Only called for
    // a SourceCode with no existing mapping, so this runs once per local code, not once per save.
    private async Task EnsureFacilityLocationAsync(string facilityId, HslocMapping mapping, CancellationToken cancellationToken)
    {
        var existing = await _normalizationClient.GetFacilityLocationAsync(facilityId, mapping.SourceCode, cancellationToken);
        if (existing.StatusCode == StatusCodes.Status404NotFound)
        {
            var createResponse = await _normalizationClient.CreateFacilityLocationAsync(
                facilityId,
                new CreateFacilityLocationRequestApiModel
                {
                    LocationId = mapping.SourceCode,
                    LocationName = mapping.SourceDisplay
                },
                cancellationToken);
            LinkResponseHandler.Require(createResponse, ServiceName, nameof(SaveAsync));
            return;
        }

        LinkResponseHandler.Require(existing, ServiceName, nameof(SaveAsync));
    }
}
