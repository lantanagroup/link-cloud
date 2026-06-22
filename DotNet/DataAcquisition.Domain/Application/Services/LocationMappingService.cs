using System.Collections.Concurrent;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
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
}

public class LocationMappingService(
    IOrganizationLocationMappingManager organizationLocationMappingManager,
    IOrganizationLocationMappingQueries organizationLocationMappingQueries,
    IOrganizationLocationConfigurationQueries organizationLocationConfigurationQueries,
    ICacheService cacheService,
    ILogger<LocationMappingService> logger) : ILocationMappingService

{
    private readonly IOrganizationLocationMappingManager _organizationLocationMappingManager =
        organizationLocationMappingManager;

    private readonly IOrganizationLocationMappingQueries _organizationLocationMappingQueries =
        organizationLocationMappingQueries;

    private readonly IOrganizationLocationConfigurationQueries _organizationLocationConfigurationQueries =
        organizationLocationConfigurationQueries;

    private readonly ICacheService _cacheService = cacheService;
    private readonly ILogger<LocationMappingService> _logger = logger;

    // FhirPath strings are validated (compiled) at config-write time and are low-cardinality per
    // facility, so caching the compiled expression avoids recompiling per Location in the acquire loop.
    private static readonly ConcurrentDictionary<string, CompiledExpression> CompiledFhirPaths = new();


    private const string OrgLocationConditionsCacheKeyPrefix = "org-location-conditions:";
    private static readonly TimeSpan OrgLocationConditionsTtl = TimeSpan.FromHours(1);

    public async Task<OrganizationLocationMappingModel> UpdateLocationMappingAsync(string facilityId, Location location,
        string? locationAlias = null, CancellationToken cancellationToken = default)
    {
        var conditions = await GetActiveConditionsForFacility(facilityId);
        if (conditions.Count == 0)
        {
            throw new NotFoundException($"FacilityId {facilityId} does not have any location mappings configured");
        }

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

    private async Task<OrganizationLocationMappingModel> UpdateMapping(Location location, bool isOrgLocation,
        OrganizationLocationMappingModel locationMapping, OrganizationLocationMappingModel? partOf)
    {
        if (locationMapping.LocationName != location.Name ||
            locationMapping.PartOfValue != location.PartOf?.Reference?.SplitReference() ||
            locationMapping.PartOfId != partOf?.LocationMappingId ||
            locationMapping.LocationAlias != location.Alias?.FirstOrDefault() ||
            locationMapping.IsOrgLocation != isOrgLocation)
        {
            // Something changed, update the record
            var updateRecord = new UpdateOrganizationLocationMappingModel
            {
                IsActive = locationMapping.IsActive,
                IsOrgLocation = isOrgLocation,
                LocationAlias = location.Alias?.FirstOrDefault(),
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

        await UpdatePartOfIdsOnRecordsWithThisPartOfValue(
            facilityId,
            location.Id,
            locationMapping.LocationMappingId,
            cancellationToken: cancellationToken);

        return locationMapping;
    }

    private async Task UpdatePartOfIdsOnRecordsWithThisPartOfValue(
        string facilityId, string? locationId, int locationMappingId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(locationId))
        {
            return;
        }

        var adopted = await _organizationLocationMappingManager
            .SetPartOfIdForChildrenAsync(facilityId, locationId, locationMappingId, cancellationToken);

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
        var cacheKey = OrgLocationConditionsCacheKeyPrefix + facilityId;

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
                return false; // empty result → no match

            // Boolean predicate (e.g. "...exists()"): honor the returned boolean.
            if (results.Count == 1 && results[0].Value is bool b)
                return b;

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

    // SQL Server: 2627 = unique constraint, 2601 = unique index
    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}