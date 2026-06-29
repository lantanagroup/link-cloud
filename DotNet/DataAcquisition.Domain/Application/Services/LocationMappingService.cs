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
    /// Removes non-org encounters (EncounterMapping.MappedToOrg == false) from the correlation's
    /// acquired-resource cache before the tail's ResourcesAcquired event is produced. The Encounter is
    /// fetched and cached by its own (ungated) primary log, so marking a dependent log NotReportable does
    /// not keep the non-org encounter out of MeasureEval; this strip does. A patient whose encounters are
    /// all non-org ends up with no cached encounter — MeasureEval evaluates no qualifying encounter and
    /// produces a non-reportable outcome — while a patient with a mix keeps only the org encounters.
    /// No-op when org-location mapping is not active for the facility.
    /// </summary>
    /// <param name="facilityId">The facility the correlation belongs to.</param>
    /// <param name="correlationId">The acquisition correlation whose cached encounters to filter.</param>
    /// <param name="patientId">The patient whose encounter mappings determine org membership.</param>
    /// <param name="cancellationToken">Used to signal a cancellation in the request.</param>
    /// <returns>The number of non-org encounters stripped from the cache.</returns>
    Task<int> StripNonOrgEncountersFromCacheAsync(string facilityId, string correlationId, string patientId, CancellationToken cancellationToken = default);

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

    // FhirPath strings are validated (compiled) at config-write time and are low-cardinality per
    // facility, so caching the compiled expression avoids recompiling per Location in the acquire loop.
    private static readonly ConcurrentDictionary<string, CompiledExpression> CompiledFhirPaths = new();


    private static readonly TimeSpan OrgLocationConditionsTtl = TimeSpan.FromHours(1);

    public async Task<OrganizationLocationMappingModel> UpdateLocationMappingAsync(string facilityId, Location location,
        string? locationAlias = null, CancellationToken cancellationToken = default)
    {
        var conditions = await GetActiveConditionsForFacility(facilityId);
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

    public async Task ReevaluateLocationMappingsAsync(string facilityId, CancellationToken cancellationToken)
    {
        var mappings = await _organizationLocationMappingQueries.GetByFacilityIdAsync(facilityId);
        if (mappings.Count == 0)
        {
            return;
        }

        var locationIds = mappings
            .Select(m => m.LocationId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (locationIds.Count == 0)
        {
            return;
        }

        // Re-evaluation only touches Locations whose bodies were cached during acquisition; mappings
        // without a cached body are left as-is and get evaluated fresh on their next acquisition.
        var cachedRecords = (await _referenceResourcesQueries.SearchAsync(new SearchReferenceResourcesModel
        {
            FacilityId = facilityId,
            ResourceType = ResourceType.Location.ToString(),
            ResourceIds = locationIds,
            PageSize = int.MaxValue
        }, cancellationToken)).Records;

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

        var isOrgLocation = await IsOrgLocationAsync(facilityId, location);

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

        return locationMapping;
    }

    public async Task<bool> IsConfigured(string facilityId, CancellationToken cancellationToken)
    {
        var mappings = await GetActiveConditionsForFacility(facilityId);

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

    public async Task<bool> IsPatientReportableAsync(string facilityId, string patientId, CancellationToken cancellationToken)
    {
        // Reportability filtering only applies when org-location mapping is active for the facility;
        // otherwise every patient is reportable and acquisition proceeds normally.
        var organizationLocationMappingIsConfigured = await _organizationLocationConfigurationQueries
            .HasActiveByFacilityIdAsync(facilityId, cancellationToken);

        if (!organizationLocationMappingIsConfigured)
        {
            return true;
        }

        var encounterMappings = await _encounterMappingQueries
            .GetByFacilityIdAndPatientIdAsync(facilityId, patientId, cancellationToken);

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

    public async Task<int> StripNonOrgEncountersFromCacheAsync(string facilityId, string correlationId, string patientId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(correlationId) || string.IsNullOrWhiteSpace(patientId))
        {
            return 0;
        }

        // Stripping only applies when org-location mapping is active; otherwise every encounter is
        // reportable and the cache must be left intact.
        var organizationLocationMappingIsConfigured = await _organizationLocationConfigurationQueries
            .HasActiveByFacilityIdAsync(facilityId, cancellationToken);
        if (!organizationLocationMappingIsConfigured)
        {
            return 0;
        }

        var cacheKey = $"{correlationId}:{ResourceType.Encounter}";
        var cachedEncounters = _resourceCache.Get(cacheKey);
        if (cachedEncounters.Count == 0)
        {
            return 0;
        }

        var encounterMappings = await _encounterMappingQueries
            .GetByFacilityIdAndPatientIdAsync(facilityId, patientId, cancellationToken);

        var nonOrgEncounterIds = encounterMappings
            .Where(mapping => !mapping.MappedToOrg)
            .Select(mapping => mapping.EncounterId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (nonOrgEncounterIds.Count == 0)
        {
            return 0;
        }

        var orgEncounters = cachedEncounters
            .Where(encounter => !nonOrgEncounterIds.Contains(encounter.Id))
            .ToList();

        var strippedCount = cachedEncounters.Count - orgEncounters.Count;
        if (strippedCount == 0)
        {
            return 0;
        }

        // UpdateCorrelationCache is an additive HashSet, so removing entries requires deleting the key
        // and rewriting it with only the org encounters. When none remain the key is left empty, so
        // Normalization/MeasureEval rehydrate no qualifying encounter for this correlation.
        _resourceCache.Delete([cacheKey]);
        if (orgEncounters.Count > 0)
        {
            _resourceCache.UpdateCorrelationCache(cacheKey, orgEncounters, ResourceType.Encounter);
        }

        _logger.LogDebug(
            "Stripped {StrippedCount} non-org encounter(s) from cache key {CacheKey} for facility {FacilityId} (patient {PatientId}); {RemainingCount} org encounter(s) remain.",
            strippedCount, cacheKey.SanitizeForLog(), facilityId.SanitizeForLog(), patientId.SanitizeForLog(), orgEncounters.Count);

        return strippedCount;
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
                    var newLocationMapping = await _organizationLocationMappingManager.CreateAsync(new CreateOrganizationLocationMappingModel
                    {
                        FacilityId = facilityId,
                        IsActive = true,
                        IsOrgLocation = false,
                        LocationId = locationId
                    });
                    organizationLocationMappingIds.Add(newLocationMapping.LocationMappingId);
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
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // A concurrent work item inserted the same (facility, location) between our null-check
            // and SaveChanges. That's expected for shared Locations — re-read and update instead of failing.
            var existing = await _organizationLocationMappingQueries
                .GetByFacilityIdAndLocationIdAsync(facilityId, location.Id);

            if (existing is null)
            {
                // not a duplicate — a real failure, rethrow
                throw;
            }

            locationMapping = await UpdateMapping(location, isOrgLocation, existing, partOf);
        }

        await UpdateParentOnRecordsWithThisPartOfValue(
            facilityId,
            location.Id,
            locationMapping.LocationMappingId,
            isOrgLocation,
            cancellationToken: cancellationToken);

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
            _logger.LogDebug("Adopted {Count} child location(s) under mapping {LocationMappingId}", adopted,
                locationMappingId);
        }
    }

    private async Task<bool> IsOrgLocationAsync(
        string facilityId,
        Location? location)
    {
        if (location is null)
        {
            return false;
        }

        var conditions = await GetActiveConditionsForFacility(facilityId);

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

    private async Task<List<OrganizationLocationConditionModel>> GetActiveConditionsForFacility(string facilityId)
    {
        var cacheKey = OrgLocationCacheKeys.Conditions(facilityId);

        var conditions = _cacheService.Get<List<OrganizationLocationConditionModel>?>(cacheKey);

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

        _cacheService.Set(cacheKey, conditions, OrgLocationConditionsTtl, ExpirationType.Absolute);

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

    // SQL Server: 2627 = unique constraint, 2601 = unique index
    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}
