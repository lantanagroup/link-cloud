using System.Collections.Concurrent;
using DataAcquisition.Domain.Application.Models;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.FhirPath;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Serializers;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Mapping;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services;

/// <summary>
/// Manages location mapping records
/// </summary>
public interface ILocationMappingService
{
    /// <summary>
    /// Checks to see if this facility has any active location mappings
    /// </summary>
    /// <param name="facilityId">The facility ID that the mapping is for</param>
    /// <param name="cancellationToken">Used to signal a cancellation in the request</param>
    /// <returns>True if the facility has location mappings</returns>
    Task<bool> IsConfigured(string facilityId, CancellationToken cancellationToken);

    /// <summary>
    /// Updates or adds the location mapping for a given facility and location. This method is called when a new location resource is acquired from the FHIR API, and is responsible for updating the mapping between the facility and the location in the database.
    /// </summary>
    /// <param name="facilityId">The facility id that the mapping is for</param>
    /// <param name="location">The location resource from the FHIR object</param>
    /// <param name="locationAlias">An alias to use for the location</param>
    /// <param name="cancellationToken">Used to signal a cancellation in the request</param>
    /// <returns>The OrganizationLocationMappingModel for the mapping</returns>
    /// <exception cref="NotFoundException">Thrown when the facility doesn't have any active mapping configurations</exception>
    Task<OrganizationLocationMappingModel> UpdateLocationMappingAsync(string facilityId, Location location, string? locationAlias = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates the encounter-location mapping for a given facility and encounter. This method is called when a new encounter resource is acquired from the FHIR API, and is responsible for updating the mapping between the facility and the encounter in the database.
    /// </summary>
    /// <param name="facilityId">The facility id that the mapping is for</param>
    /// <param name="encounter">The encounter resource from the FHIR object</param>
    /// <param name="cancellationToken">Used to signal a cancellation in the request</param>
    /// <returns>The EncounterMappingModel for the mapping</returns>
    Task<EncounterMappingModel?> UpdateEncounterLocationMappingAsync(string facilityId, Encounter encounter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates organization-location mapping state for acquired Location and Encounter resources when
    /// location mapping is configured for the facility. Non-location/encounter resources are ignored.
    /// </summary>
    /// <returns>Location mapping updates produced for Location resources; Encounter updates do not return a mapping model.</returns>
    Task<List<OrganizationLocationMappingModel>> UpdateResourceMappingsAsync(
        string facilityId,
        IReadOnlyCollection<Resource> resources,
        CancellationToken cancellationToken = default);

    Task<List<Resource>> FilterResourcesByEncounterMappingAsync(
        string facilityId,
        IReadOnlyCollection<Resource> resources,
        CancellationToken cancellationToken);

    /// <summary>
    /// Determines whether a patient is reportable: i.e. has at least one encounter mapped to an
    /// organization location. Used to preempt further acquisition for patients whose encounters are
    /// all non-org (non-reportable).
    /// </summary>
    /// <param name="facilityId">The facility the patient belongs to.</param>
    /// <param name="patientId">The patient to evaluate.</param>
    /// <param name="cancellationToken">Used to signal a cancellation in the request.</param>
    /// <returns>
    /// True if the patient is reportable — org-location mapping is not active for the facility, the
    /// patient's encounter mappings have not been recorded yet (fail-open), or at least one encounter
    /// is mapped to the organization. False only when the patient has encounter mappings and none of
    /// them map to the organization.
    /// </returns>
    Task<bool> IsPatientReportableAsync(string facilityId, string patientId, CancellationToken cancellationToken);

    /// <summary>
    /// Determines whether a patient is reportable for a specific report encounter set.
    /// </summary>
    /// <param name="facilityId">The facility the patient belongs to.</param>
    /// <param name="patientId">The patient to evaluate.</param>
    /// <param name="currentReportEncounterIds">Encounter ids acquired for the current report.</param>
    /// <param name="cancellationToken">Used to signal a cancellation in the request.</param>
    /// <returns>
    /// True if the report has at least one org-mapped encounter, or if report encounter ids/mappings
    /// are not available yet. False only when current-report encounter mappings exist and none are org-mapped.
    /// </returns>
    Task<bool> IsPatientReportableAsync(
        string facilityId,
        string patientId,
        IReadOnlyCollection<string>? currentReportEncounterIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes non-org encounters (EncounterMapping.MappedToOrg == false) from the correlation's
    /// acquired-resource cache before the tail's ResourcesAcquired event is produced. The Encounter is
    /// fetched and cached by its own (ungated) primary log, so marking a dependent log NotReportable does
    /// not keep the non-org encounter out of MeasureEval; this strip does. A patient whose encounters are
    /// all non-org ends up with no cached encounter. The tail finalizer then omits that empty
    /// Encounter key from ResourcesAcquired so Normalization is not pointed at an empty location.
    /// MeasureEval evaluates no qualifying encounter and produces a non-reportable outcome.
    /// A patient with a mix keeps only the org encounters. No-op when org-location mapping
    /// is not active for the facility.
    /// <para>
    /// Also reports how the patient resolved, so the result can be recorded against the report it was
    /// acquired for. The outcome is scoped to this correlation's encounters rather than to every encounter
    /// the patient has ever had at the facility, so it describes one report rather than their history.
    /// </para>
    /// </summary>
    /// <param name="facilityId">The facility the correlation belongs to.</param>
    /// <param name="correlationId">The acquisition correlation whose cached encounters to filter.</param>
    /// <param name="patientId">The patient whose encounter mappings determine org membership.</param>
    /// <param name="cancellationToken">Used to signal a cancellation in the request.</param>
    /// <returns>
    /// The patient's org-location resolution for this correlation: its status, the encounter counts behind
    /// it, and the locations those encounters referenced. <see cref="LocationOrgStatus.NotApplicable"/>
    /// with zero counts when org-location mapping is not active for the facility, when there is no patient
    /// to evaluate, or when the correlation acquired no encounters.
    /// </returns>
    Task<LocationOrgOutcome> StripNonOrgEncountersFromCacheAsync(
        string facilityId, string correlationId, string patientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates and upserts a batch of Location resources for a facility — the shared routine used by
    /// both acquisition and re-evaluation. Each Location's IsOrgLocation is recomputed against the
    /// active conditions and its mapping is added/updated, cascading MappedToOrg to linked encounter
    /// mappings. Does not require active conditions (none → every Location evaluates as non-org).
    /// </summary>
    Task UpdateLocationMappingsAsync(string facilityId, IReadOnlyCollection<Location> locations, CancellationToken cancellationToken);

    /// <summary>
    /// Re-evaluates the facility's existing Location mappings against the current conditions using the
    /// Location bodies cached in ReferenceResources. Called when a facility's org-location configuration
    /// changes so already-acquired Locations don't keep a stale IsOrgLocation/MappedToOrg. Mappings with
    /// no cached body are left untouched and re-evaluated fresh on their next acquisition.
    /// </summary>
    Task ReevaluateLocationMappingsAsync(string facilityId, CancellationToken cancellationToken);
}

public class LocationMappingService(
    IOrganizationLocationMappingManager organizationLocationMappingManager,
    IOrganizationLocationMappingQueries organizationLocationMappingQueries,
    IOrganizationLocationConfigurationQueries organizationLocationConfigurationQueries,
    IEncounterMappingQueries encounterMappingQueries,
    IEncounterMappingManager encounterMappingManager,
    IReferenceResourcesQueries referenceResourcesQueries,
    ICacheService cacheService,
    IResourceCache resourceCache,
    ILogger<LocationMappingService> logger) : ILocationMappingService

{
    private readonly IOrganizationLocationMappingManager _organizationLocationMappingManager =
        organizationLocationMappingManager;

    private readonly IOrganizationLocationMappingQueries _organizationLocationMappingQueries =
        organizationLocationMappingQueries;

    private readonly IOrganizationLocationConfigurationQueries _organizationLocationConfigurationQueries =
        organizationLocationConfigurationQueries;

    private readonly IEncounterMappingQueries _encounterMappingQueries = encounterMappingQueries;
    private readonly IEncounterMappingManager _encounterMappingManager = encounterMappingManager;
    private readonly IReferenceResourcesQueries _referenceResourcesQueries = referenceResourcesQueries;
    private readonly ICacheService _cacheService = cacheService;
    private readonly IResourceCache _resourceCache = resourceCache;
    private readonly ILogger<LocationMappingService> _logger = logger;

    // Reported when there is nothing to resolve: org-location mapping is not active for the facility, or
    // no patient was supplied. LocationOrgOutcome is immutable, so one shared instance is safe.
    private static readonly LocationOrgOutcome NotApplicableOutcome = new(
        Status: LocationOrgStatus.NotApplicable,
        EncounterCount: 0,
        OrgEncounterCount: 0,
        AssumedOrgEncounterCount: 0,
        Matches: []);

    // FhirPath strings are validated (compiled) at config-write time and are low-cardinality per
    // facility, so caching the compiled expression avoids recompiling per Location in the acquire loop.
    private static readonly ConcurrentDictionary<string, CompiledExpression> CompiledFhirPaths = new();


    private static readonly TimeSpan OrgLocationConditionsTtl = TimeSpan.FromHours(1);

    public async Task<OrganizationLocationMappingModel> UpdateLocationMappingAsync(string facilityId, Location location,
        string? locationAlias = null, CancellationToken cancellationToken = default)
    {
        var conditions = await GetActiveConditionsForFacility(facilityId, cancellationToken);
        if (conditions.Count == 0)
        {
            throw new NotFoundException($"FacilityId {facilityId} does not have any location mappings configured");
        }

        return await UpsertLocationMappingAsync(facilityId, location, locationAlias, cancellationToken);
    }

    public async Task UpdateLocationMappingsAsync(string facilityId, IReadOnlyCollection<Location> locations,
        CancellationToken cancellationToken)
    {
        foreach (var location in locations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await UpsertLocationMappingAsync(facilityId, location, locationAlias: null, cancellationToken);
        }
    }

    public async Task<List<OrganizationLocationMappingModel>> UpdateResourceMappingsAsync(
        string facilityId,
        IReadOnlyCollection<Resource> resources,
        CancellationToken cancellationToken = default)
    {
        var updatedLocationMappings = new List<OrganizationLocationMappingModel>();

        if (resources.Count == 0 || !await IsConfigured(facilityId, cancellationToken))
        {
            return updatedLocationMappings;
        }

        foreach (var resource in resources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (resource)
            {
                case Location location:
                    updatedLocationMappings.Add(await UpsertLocationMappingAsync(
                        facilityId,
                        location,
                        locationAlias: null,
                        cancellationToken));
                    break;
                case Encounter encounter:
                    await UpdateEncounterLocationMappingAsync(facilityId, encounter, cancellationToken);
                    break;
            }
        }

        return updatedLocationMappings;
    }

    public async Task ReevaluateLocationMappingsAsync(string facilityId, CancellationToken cancellationToken)
    {
        var mappings = await _organizationLocationMappingQueries.GetByFacilityIdAsync(facilityId);

        var locationIds = mappings
            .Select(m => m.LocationId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var search = new SearchReferenceResourcesModel
        {
            FacilityId = facilityId,
            ResourceType = ResourceType.Location.ToString(),
            PageSize = int.MaxValue
        };

        if (locationIds.Count > 0)
        {
            search.ResourceIds = locationIds;
        }

        // Re-evaluation touches cached Location bodies. If no mapping rows exist yet (for example,
        // mapping was disabled during the earlier acquisition), hydrate mappings from every cached
        // Location for the facility instead of returning early.
        var cachedRecords = (await _referenceResourcesQueries.SearchAsync(search, cancellationToken)).Records;

        var locations = new List<Location>();
        foreach (var record in cachedRecords)
        {
            try
            {
                if (FhirResourceDeserializer.DeserializeFhirResource(record) is Location location)
                {
                    locations.Add(location);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "ReevaluateLocationMappingsAsync: failed to deserialize cached Location {ResourceId} for facility {FacilityId}; skipping.",
                    record.ResourceId.SanitizeForLog(), facilityId.SanitizeForLog());
            }
        }

        if (locations.Count == 0)
        {
            return;
        }

        await UpdateLocationMappingsAsync(facilityId, locations, cancellationToken);
        await RecomputeCachedLocationHierarchyAsync(facilityId, locations, cancellationToken);
        await PropagateOrgLocationToDescendantsAsync(facilityId, cancellationToken);

        _logger.LogDebug(
            "Re-evaluated {Count} cached Location(s) for facility {FacilityId} after a configuration change.",
            locations.Count, facilityId.SanitizeForLog());
    }

    /// <summary>
    /// Core evaluate-and-upsert for a single Location: resolves any existing mapping and PartOf parent,
    /// re-evaluates IsOrgLocation against the active conditions, and adds or updates the mapping (the
    /// update path cascades MappedToOrg to linked encounter mappings). Unlike the public
    /// <see cref="UpdateLocationMappingAsync"/>, this does NOT require active conditions — with none,
    /// IsOrgLocationAsync returns false, which correctly demotes the location to non-org.
    /// </summary>
    private async Task<OrganizationLocationMappingModel> UpsertLocationMappingAsync(string facilityId, Location location,
        string? locationAlias, CancellationToken cancellationToken)
    {
        var locationMapping =
            await _organizationLocationMappingQueries.GetByFacilityIdAndLocationIdAsync(
                facilityId: facilityId,
                locationId: location.Id);

        // See if the PartOf has an record already so we can set the Id on the location mapping
        OrganizationLocationMappingModel? partOf = null;
        if (location.PartOf?.Reference != null)
        {
            partOf = await _organizationLocationMappingQueries.GetByFacilityIdAndLocationIdAsync(
                facilityId: facilityId,
                locationId: location.PartOf.Reference.SplitReference());
        }

        var isOrgLocation = await IsOrgLocationAsync(facilityId, location, cancellationToken) || partOf?.IsOrgLocation == true;

        if (locationMapping is null)
        {
            locationMapping = await AddMapping(
                facilityId,
                location,
                isOrgLocation,
                locationAlias,
                partOf,
                cancellationToken);
        }
        else
        {
            locationMapping = await UpdateMapping(location, isOrgLocation, locationMapping, partOf);
        }

        await UpdateParentOnRecordsWithThisPartOfValue(
            facilityId,
            location.Id,
            locationMapping.LocationMappingId,
            isOrgLocation,
            cancellationToken: cancellationToken);

        return locationMapping;
    }

    public async Task<bool> IsConfigured(string facilityId, CancellationToken cancellationToken)
    {
        var mappings = await GetActiveConditionsForFacility(facilityId, cancellationToken);

        return mappings.Count != 0;
    }

    public async Task<List<Resource>> FilterResourcesByEncounterMappingAsync(
        string facilityId,
        IReadOnlyCollection<Resource> resources,
        CancellationToken cancellationToken)
    {
        if (resources.Count == 0)
        {
            return resources.ToList();
        }

        var resourceEncounterIds = resources
            .Select(resource => new
            {
                Resource = resource,
                EncounterIds = GetEncounterReferenceIds(resource)
            })
            .ToList();

        var encounterIds = resourceEncounterIds
            .SelectMany(x => x.EncounterIds)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (encounterIds.Count == 0)
        {
            return resources.ToList();
        }

        var organizationLocationMappingIsConfigured = await _organizationLocationConfigurationQueries
            .HasActiveByFacilityIdAsync(facilityId, cancellationToken);

        if (!organizationLocationMappingIsConfigured)
        {
            return resources.ToList();
        }

        var encounterMappings = await _encounterMappingQueries.GetByFacilityIdAndEncounterIdsAsync(
            facilityId,
            encounterIds,
            cancellationToken);

        var mappedEncounterIds = encounterMappings
            .Where(mapping => mapping.MappedToOrg)
            .Select(mapping => mapping.EncounterId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var filteredResources = resourceEncounterIds
            .Where(x => x.EncounterIds.Count == 0 || x.EncounterIds.Any(mappedEncounterIds.Contains))
            .Select(x => x.Resource)
            .ToList();

        var removedCount = resources.Count - filteredResources.Count;
        if (removedCount > 0)
        {
            _logger.LogDebug(
                "Removed {RemovedCount} resource(s) without mapped encounter organization for facility {FacilityId}.",
                removedCount,
                facilityId);
        }

        return filteredResources;
    }

    public Task<bool> IsPatientReportableAsync(string facilityId, string patientId, CancellationToken cancellationToken) =>
        IsPatientReportableAsync(facilityId, patientId, currentReportEncounterIds: null, cancellationToken);

    public async Task<bool> IsPatientReportableAsync(
        string facilityId,
        string patientId,
        IReadOnlyCollection<string>? currentReportEncounterIds,
        CancellationToken cancellationToken)
    {
        // Reportability filtering only applies when org-location mapping is active for the facility;
        // otherwise every patient is reportable and acquisition proceeds normally.
        var organizationLocationMappingIsConfigured = await _organizationLocationConfigurationQueries
            .HasActiveByFacilityIdAsync(facilityId, cancellationToken);

        if (!organizationLocationMappingIsConfigured)
        {
            return true;
        }

        List<EncounterMappingModel> encounterMappings;
        if (currentReportEncounterIds is not null)
        {
            var currentReportEncounterIdSet = currentReportEncounterIds
                .Select(NormalizeResourceId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Fail open: without usable current-report encounter ids we cannot conclude the patient
            // is non-reportable for this report.
            if (currentReportEncounterIdSet.Count == 0)
            {
                return true;
            }

            encounterMappings = await _encounterMappingQueries
                .GetByFacilityIdAndEncounterIdsAsync(facilityId, currentReportEncounterIdSet, cancellationToken);

            encounterMappings = encounterMappings
                .Where(mapping => string.Equals(mapping.PatientId, patientId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        else
        {
            encounterMappings = await _encounterMappingQueries
                .GetByFacilityIdAndPatientIdAsync(facilityId, patientId, cancellationToken);
        }

        // Fail open: with no encounter mappings recorded yet we cannot conclude the patient is
        // non-reportable (e.g. the encounters have not been acquired/mapped), so keep acquiring.
        if (encounterMappings.Count == 0)
        {
            return true;
        }

        var reportable = encounterMappings.Any(mapping => mapping.MappedToOrg);

        if (!reportable)
        {
            _logger.LogDebug(
                "Patient {PatientId} for facility {FacilityId} has no encounters mapped to an organization location; treating as non-reportable.",
                patientId.SanitizeForLog(), facilityId.SanitizeForLog());
        }

        return reportable;
    }

    public async Task<LocationOrgOutcome> StripNonOrgEncountersFromCacheAsync(
        string facilityId, string correlationId, string patientId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(correlationId) || string.IsNullOrWhiteSpace(patientId))
        {
            return NotApplicableOutcome;
        }

        // Stripping only applies when org-location mapping is active; otherwise every encounter is
        // reportable and the cache must be left intact.
        var organizationLocationMappingIsConfigured = await _organizationLocationConfigurationQueries
            .HasActiveByFacilityIdAsync(facilityId, cancellationToken);
        if (!organizationLocationMappingIsConfigured)
        {
            return NotApplicableOutcome;
        }

        // The cached encounters are this correlation's encounters, so they also bound what the outcome
        // describes. Nothing cached means nothing was acquired for this patient in this correlation, which
        // is not a failure to resolve — there was nothing to resolve.
        var cacheKey = $"{correlationId}:{ResourceType.Encounter}";
        var cachedEncounters = await _resourceCache.GetAsync(cacheKey, cancellationToken);
        if (cachedEncounters.Count == 0)
        {
            return NotApplicableOutcome;
        }

        // GetByFacilityIdAndPatientIdAsync returns every mapping the patient has ever had at this facility,
        // across every report they have appeared in. Scope it to the encounters this correlation actually
        // acquired so the outcome describes this report rather than the patient's whole history.
        var cachedEncounterIds = cachedEncounters
            .Select(encounter => encounter.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var encounterMappings = (await _encounterMappingQueries
                .GetByFacilityIdAndPatientIdAsync(facilityId, patientId, cancellationToken))
            .Where(mapping => cachedEncounterIds.Contains(mapping.EncounterId))
            .ToList();

        var outcome = BuildLocationOrgOutcome(encounterMappings);

        // Scoped to the same set, which changes nothing about the strip: a mapping for an encounter absent
        // from the cache could never have matched anything being filtered.
        var nonOrgEncounterIds = encounterMappings
            .Where(mapping => !mapping.MappedToOrg)
            .Select(mapping => mapping.EncounterId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (nonOrgEncounterIds.Count == 0)
        {
            return outcome;
        }

        var orgEncounters = cachedEncounters
            .Where(encounter => !nonOrgEncounterIds.Contains(encounter.Id))
            .ToList();

        var strippedCount = cachedEncounters.Count - orgEncounters.Count;
        if (strippedCount == 0)
        {
            return outcome;
        }

        // UpdateCorrelationCacheAsync is an additive HashSet, so removing entries requires deleting the key
        // and rewriting it with only the org encounters. When none remain the key is left empty, so
        // Normalization/MeasureEval rehydrate no qualifying encounter for this correlation.
        await _resourceCache.DeleteAsync([cacheKey], cancellationToken);
        if (orgEncounters.Count > 0)
        {
            await _resourceCache.UpdateCorrelationCacheAsync(cacheKey, orgEncounters, ResourceType.Encounter, cancellationToken);
        }

        _logger.LogDebug(
            "Stripped {StrippedCount} non-org encounter(s) from cache key {CacheKey} for facility {FacilityId} (patient {PatientId}); {RemainingCount} org encounter(s) remain.",
            strippedCount, cacheKey.SanitizeForLog(), facilityId.SanitizeForLog(), patientId.SanitizeForLog(), orgEncounters.Count);

        return outcome;
    }

    /// <summary>
    /// Projects the encounter mappings for one correlation into the org-location outcome reported
    /// alongside that correlation's acquired resources.
    /// </summary>
    /// <remarks>
    /// Reads only what <see cref="IEncounterMappingQueries.GetByFacilityIdAndPatientIdAsync"/> already
    /// returned, so it issues no query of its own.
    /// </remarks>
    /// <param name="encounterMappings">
    /// The patient's mappings, already narrowed to the encounters this correlation acquired. Passing the
    /// unscoped result would make every count describe the patient's history rather than this report.
    /// </param>
    private static LocationOrgOutcome BuildLocationOrgOutcome(
        IReadOnlyCollection<EncounterMappingModel> encounterMappings)
    {
        var orgEncounterCount = encounterMappings.Count(mapping => mapping.MappedToOrg);

        // An encounter with no location rows is one that carried no resolvable location references, which
        // UpdateEncounterLocationMappingAsync treats as belonging to the org by default. Counted apart from
        // the rest so membership that was never verified against the facility's configuration stays
        // distinguishable from membership that was.
        var assumedOrgEncounterCount = encounterMappings
            .Count(mapping => mapping.MappedToOrg && mapping.EncounterLocations.Count == 0);

        // No mapping rows for any of this correlation's encounters means none were evaluated, which is not
        // a failure to resolve — there was nothing to resolve.
        var status = encounterMappings.Count == 0
            ? LocationOrgStatus.NotApplicable
            : orgEncounterCount > 0
                ? LocationOrgStatus.Found
                : LocationOrgStatus.NotFound;

        // Deduplicated on the mapping's own key: a patient with a dozen encounters in one ward references
        // that location a dozen times, and the outcome describes locations rather than visits.
        var matches = encounterMappings
            .SelectMany(mapping => mapping.EncounterLocations)
            .Where(location => location.LocationId is not null)
            .DistinctBy(location => location.OrganizationLocationMappingId)
            .Select(location => new LocationOrgMatch(
                location.LocationId!,
                location.LocationName,
                location.LocationAlias,
                location.PartOfValue,
                location.IsOrgLocation))
            .ToList();

        return new LocationOrgOutcome(
            Status: status,
            EncounterCount: encounterMappings.Count,
            OrgEncounterCount: orgEncounterCount,
            AssumedOrgEncounterCount: assumedOrgEncounterCount,
            Matches: matches);
    }

    public async Task<EncounterMappingModel?> UpdateEncounterLocationMappingAsync(string facilityId, Encounter encounter, CancellationToken cancellationToken = default)
    {
        // if there is already an encounter mapping for this encounter, delete it and create a new one. 
        // This is because the encounter may have changed its location references, so we need to re-evaluate the mapping.
        _logger.LogDebug("Duplicate encounter mapping for encounter {EncounterId} in facility {FacilityId} detected. Deleting existing mapping and creating a new one.", encounter.Id.SanitizeForLog(), facilityId.SanitizeForLog());
        await _encounterMappingManager.DeleteByEncounterIdAndFacilityIdAsync(encounter.Id, facilityId);

        var locationIds = encounter.Location
            .Select(locationComponent => locationComponent.Location?.Reference?.SplitReference())
            .Where(locationId => !string.IsNullOrWhiteSpace(locationId))
            .Select(locationId => locationId!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var patientId = encounter.Subject?.Reference?.SplitReference();
        if(patientId is null)
        {
            _logger.LogWarning("Encounter {EncounterId} for facility {FacilityId} does not have a subject reference (patient ID), skipping mapping.", encounter.Id.SanitizeForLog(), facilityId.SanitizeForLog());
            return null;
        }

        var mappedToOrg = false;
        var organizationLocationMappingIds = new List<int>();
        if(!locationIds.Any()) //encounter does not have any location references, so we have to assume it is part of the org by default
        {
            mappedToOrg = true;
            _logger.LogDebug("Encounter {EncounterId} for facility {FacilityId} has no location references, assuming it is part of the organization.", encounter.Id.SanitizeForLog(), facilityId.SanitizeForLog());
        }
        else
        {
            //use the locationIds to get any existing location mappings and see if they are mapped to the org
            var existingMappings = await organizationLocationMappingQueries.GetByFacilityIdAsync(facilityId);
            foreach(var locationId in locationIds)
            {
                var existingMapping = existingMappings.FirstOrDefault(m => m.LocationId == locationId);
                if(existingMapping is not null)
                {
                    // OR-accumulate: the encounter is mapped to the org if ANY referenced location
                    // is an org location. A plain assignment here would let a trailing non-org
                    // location overwrite an earlier org match.
                    mappedToOrg = mappedToOrg || existingMapping.IsOrgLocation;
                    organizationLocationMappingIds.Add(existingMapping.LocationMappingId);
                }
                else
                {
                    // We haven't seen this location before, so we need to create a new mapping for it.
                    // We'll assume it is not part of the org by default. This can change once the location is acquired and the conditions are evaluated.
                    _logger.LogDebug("Encounter {EncounterId} for facility {FacilityId} has a location reference {LocationId} that does not have an existing mapping. Creating one...", encounter.Id.SanitizeForLog(), facilityId.SanitizeForLog(), locationId.SanitizeForLog());
                    OrganizationLocationMappingModel newLocationMapping;
                    try
                    {
                        newLocationMapping = await _organizationLocationMappingManager.CreateAsync(new CreateOrganizationLocationMappingModel
                        {
                            FacilityId = facilityId,
                            IsActive = true,
                            IsOrgLocation = false,
                            LocationId = locationId
                        });
                    }
                    catch (Exception ex) when (IsUniqueConstraintViolation(ex))
                    {
                        // Another worker may insert the same (facility, location) mapping concurrently.
                        // Re-read and continue instead of failing the entire acquisition log.
                        var existingAfterDuplicate = await _organizationLocationMappingQueries
                            .GetByFacilityIdAndLocationIdAsync(facilityId, locationId);

                        if (existingAfterDuplicate is null)
                        {
                            throw;
                        }

                        newLocationMapping = existingAfterDuplicate;
                    }

                    organizationLocationMappingIds.Add(newLocationMapping.LocationMappingId);
                    mappedToOrg = mappedToOrg || newLocationMapping.IsOrgLocation;
                }
            }
        }
        
        return await _encounterMappingManager.CreateAsync(new CreateEncounterMappingModel
            {
                FacilityId = facilityId,
                PatientId = patientId,
                EncounterId = encounter.Id,
                MappedToOrg = mappedToOrg,
                OrganizationLocationMappingIds = organizationLocationMappingIds
            });
    }

    private async Task<OrganizationLocationMappingModel> UpdateMapping(Location location, bool isOrgLocation,
        OrganizationLocationMappingModel locationMapping, OrganizationLocationMappingModel? partOf)
    {
        var incomingAlias = location.Alias?.FirstOrDefault();

        // Only treat the alias as changed when the resource actually carries one that differs.
        // A missing alias is preserved (UpdateByIdAsync ignores a null alias), so comparing the
        // stored alias against null would otherwise fire a redundant update on every re-acquire.
        if (locationMapping.LocationName != location.Name ||
            locationMapping.PartOfValue != location.PartOf?.Reference?.SplitReference() ||
            locationMapping.PartOfId != partOf?.LocationMappingId ||
            (incomingAlias != null && locationMapping.LocationAlias != incomingAlias) ||
            locationMapping.IsOrgLocation != isOrgLocation)
        {
            // Something changed, update the record
            var updateRecord = new UpdateOrganizationLocationMappingModel
            {
                IsActive = locationMapping.IsActive,
                IsOrgLocation = isOrgLocation,
                LocationAlias = incomingAlias,
                LocationName = location.Name,
                PartOfValue = location.PartOf?.Reference?.SplitReference(),
                PartOfId = partOf?.LocationMappingId
            };

            locationMapping =
                await _organizationLocationMappingManager.UpdateByIdAsync(
                    locationMapping.LocationMappingId,
                    updateRecord);
        }

        return locationMapping;
    }

    private async Task<OrganizationLocationMappingModel> AddMapping(
        string facilityId,
        Location location,
        bool isOrgLocation,
        string? locationAlias,
        OrganizationLocationMappingModel? partOf,
        CancellationToken cancellationToken)
    {
        var newMapping = new CreateOrganizationLocationMappingModel
        {
            FacilityId = facilityId,
            IsActive = true,
            IsOrgLocation = isOrgLocation,
            LocationAlias = locationAlias ?? location.Alias?.FirstOrDefault() ?? location.Name,
            LocationId = location.Id,
            LocationName = location.Name,
            PartOfValue = location.PartOf?.Reference.SplitReference(),
            PartOfId = partOf?.LocationMappingId
        };

        OrganizationLocationMappingModel locationMapping;
        try
        {
            locationMapping = await _organizationLocationMappingManager.CreateAsync(newMapping);
        }
        catch (Exception ex) when (IsUniqueConstraintViolation(ex))
        {
            // A concurrent work item inserted the same (facility, location) between our null-check
            // and SaveChanges. That's expected for shared Locations — re-read and update instead of failing.
            var existing = await _organizationLocationMappingQueries
                .GetByFacilityIdAndLocationIdAsync(facilityId, location.Id);

            if (existing is null)
                throw;

            locationMapping = await UpdateMapping(location, isOrgLocation, existing, partOf);
        }

        return locationMapping;
    }

    private async Task UpdateParentOnRecordsWithThisPartOfValue(
        string facilityId, string? locationId, int locationMappingId, bool isOrgLocation, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(locationId))
        {
            return;
        }

        var adopted = await _organizationLocationMappingManager
            .SetParentForChildrenAsync(facilityId, locationId, locationMappingId, isOrgLocation, cancellationToken);

        if (adopted > 0)
        {
            _logger.LogDebug("Adopted {Count} descendant location(s) under mapping {LocationMappingId}", adopted,
                locationMappingId);
        }
    }

    private async Task PropagateOrgLocationToDescendantsAsync(string facilityId, CancellationToken cancellationToken)
    {
        var mappings = await _organizationLocationMappingQueries.GetByFacilityIdAsync(facilityId);
        var maxPasses = mappings.Count;

        for (var pass = 0; pass < maxPasses; pass++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var updates = 0;
            var orgMappings = mappings
                .Where(mapping => mapping.IsOrgLocation && !string.IsNullOrWhiteSpace(mapping.LocationId))
                .ToList();

            foreach (var mapping in orgMappings)
            {
                updates += await _organizationLocationMappingManager.SetParentForChildrenAsync(
                    facilityId,
                    mapping.LocationId!,
                    mapping.LocationMappingId,
                    mapping.IsOrgLocation,
                    cancellationToken);
            }

            if (updates == 0)
            {
                return;
            }

            mappings = await _organizationLocationMappingQueries.GetByFacilityIdAsync(facilityId);
        }
    }

    private async Task RecomputeCachedLocationHierarchyAsync(
        string facilityId,
        IReadOnlyCollection<Location> locations,
        CancellationToken cancellationToken)
    {
        var cachedLocations = locations
            .Where(location => !string.IsNullOrWhiteSpace(location.Id))
            .GroupBy(location => location.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        if (cachedLocations.Count == 0)
        {
            return;
        }

        var directOrgById = new Dictionary<string, bool>(StringComparer.Ordinal);
        var parentById = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var location in cachedLocations.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            directOrgById[location.Id] = await IsOrgLocationAsync(facilityId, location, cancellationToken);
            parentById[location.Id] = location.PartOf?.Reference?.SplitReference();
        }

        var effectiveOrgById = new Dictionary<string, bool>(StringComparer.Ordinal);

        bool ResolveEffectiveOrg(string locationId, HashSet<string> visiting)
        {
            if (effectiveOrgById.TryGetValue(locationId, out var cachedValue))
            {
                return cachedValue;
            }

            if (!directOrgById.TryGetValue(locationId, out var isOrgLocation))
            {
                return false;
            }

            if (!visiting.Add(locationId))
            {
                return isOrgLocation;
            }

            if (parentById.TryGetValue(locationId, out var parentLocationId)
                && !string.IsNullOrWhiteSpace(parentLocationId)
                && ResolveEffectiveOrg(parentLocationId, visiting))
            {
                isOrgLocation = true;
            }

            visiting.Remove(locationId);
            effectiveOrgById[locationId] = isOrgLocation;
            return isOrgLocation;
        }

        foreach (var locationId in directOrgById.Keys)
        {
            ResolveEffectiveOrg(locationId, []);
        }

        var mappingsByLocationId = (await _organizationLocationMappingQueries.GetByFacilityIdAsync(facilityId))
            .Where(mapping => !string.IsNullOrWhiteSpace(mapping.LocationId))
            .ToDictionary(mapping => mapping.LocationId!, StringComparer.Ordinal);

        foreach (var (locationId, isOrgLocation) in effectiveOrgById)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!mappingsByLocationId.TryGetValue(locationId, out var mapping)
                || mapping.IsOrgLocation == isOrgLocation)
            {
                continue;
            }

            await _organizationLocationMappingManager.UpdateByIdAsync(
                mapping.LocationMappingId,
                new UpdateOrganizationLocationMappingModel
                {
                    IsOrgLocation = isOrgLocation
                });
        }
    }

    private async Task<bool> IsOrgLocationAsync(
        string facilityId,
        Location? location,
        CancellationToken cancellationToken)
    {
        if (location is null)
        {
            return false;
        }

        var conditions = await GetActiveConditionsForFacility(facilityId, cancellationToken);

        if (conditions.Count == 0)
        {
            return false;
        }

        var element = location.ToTypedElement();

        // A location is an org location if it matches
        // ANY active condition. Priority is not decisive — it only affects evaluation order, so we
        // stop at the first match.
        foreach (var condition in conditions)
        {
            if (!EvaluatesTrue(element, condition.FhirPath!))
            {
                continue;
            }

            _logger.LogDebug(
                "Location {LocationId} matched org-location condition {ConditionId} (priority {Priority})",
                location.Id, condition.ConditionId, condition.Priority);

            return true;
        }

        return false;
    }

    private async Task<List<OrganizationLocationConditionModel>> GetActiveConditionsForFacility(string facilityId, CancellationToken cancellationToken)
    {
        var cacheKey = OrgLocationCacheKeys.Conditions(facilityId);

        var conditions = await _cacheService.GetAsync<List<OrganizationLocationConditionModel>?>(cacheKey, cancellationToken);

        if (conditions is not null)
        {
            return conditions;
        }

        var configs = await _organizationLocationConfigurationQueries.GetByFacilityIdAsync(facilityId);

        conditions = configs
            .Where(c => c.IsActive)
            .SelectMany(c => c.Conditions)
            .Where(c => !string.IsNullOrWhiteSpace(c.FhirPath))
            .OrderBy(c => c.Priority)
            .ToList();

        await _cacheService.SetAsync(cacheKey, conditions, OrgLocationConditionsTtl, ExpirationType.Absolute, cancellationToken);

        return conditions;
    }

    private bool EvaluatesTrue(ITypedElement element, string fhirPath)
    {
        try
        {
            var compiled = CompiledFhirPaths.GetOrAdd(fhirPath, fp => new FhirPathCompiler().Compile(fp));
            var results = compiled(element, new EvaluationContext()).ToList();

            if (results.Count == 0)
            {
                // empty result → no match
                return false; 
            }

            // Boolean predicate (e.g. "...exists()"): honor the returned boolean.
            if (results.Count == 1 && results[0].Value is bool isMatch)
            {
                return isMatch;
            }

            // Node-selecting expression (e.g. "identifier.where(system=...)"): non-empty → match.
            return true;
        }
        catch (Exception exception)
        {
            // Conditions are syntax-validated when the config is saved, so a runtime failure here
            // means the expression didn't fit this resource shape — treat as no-match, not fatal.
            _logger.LogDebug("Error searching on fhirPath {FhirPath}:{Message}", fhirPath, exception.Message);
            return false;
        }
    }

    private static List<string> GetEncounterReferenceIds(Resource resource)
    {
        // Encounter resources themselves must not be filtered by their internal encounter-to-encounter
        // links (for example Encounter.partOf). During initial acquisition those child encounters are
        // needed to establish encounter/location mappings; treating partOf as an external encounter
        // dependency can drop valid child encounters before mapping has been computed.
        if (resource is Encounter)
        {
            return [];
        }

        return ReferenceResourceBundleExtractor
            .Extract(resource, [ResourceType.Encounter.ToString()])
            .Select(GetEncounterReferenceId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? GetEncounterReferenceId(ResourceReference reference)
    {
        if (string.IsNullOrWhiteSpace(reference.Reference))
        {
            return null;
        }

        try
        {
            var identity = new ResourceIdentity(reference.Reference);
            if (string.Equals(identity.ResourceType, ResourceType.Encounter.ToString(), StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(identity.Id))
            {
                return identity.Id;
            }
        }
        catch (Exception)
        {
        }

        if (string.Equals(reference.Type.SplitReference(), ResourceType.Encounter.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return reference.Reference.SplitReference();
        }

        return null;
    }

    private static string? NormalizeResourceId(string? resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return null;
        }

        return resourceId.SplitReference();
    }

    // SQL Server: 2627 = unique constraint, 2601 = unique index.
    // EF/database providers can wrap SqlException multiple levels deep, so walk the chain.
    private static bool IsUniqueConstraintViolation(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 2601 or 2627 })
                return true;

            if (current is SqlException sqlEx
                && sqlEx.Message.Contains("UQ_LocationMapping_Facility_Location", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
