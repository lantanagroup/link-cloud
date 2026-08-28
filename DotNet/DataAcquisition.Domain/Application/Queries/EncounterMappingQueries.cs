using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using System.Linq.Expressions;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;

public interface IEncounterMappingQueries
{
    Task<EncounterMappingModel?> GetByIdAsync(int id);
    Task<List<EncounterMappingModel>> GetByFacilityIdAsync(string facilityId);
    Task<List<EncounterMappingModel>> GetByPatientIdAsync(string patientId);
    Task<EncounterMappingModel?> GetByEncounterIdAsync(string encounterId);
    Task<EncounterMappingModel?> GetByFacilityIdAndEncounterIdAsync(string facilityId, string encounterId);
    Task<List<EncounterMappingModel>> GetByFacilityIdAndEncounterIdsAsync(string facilityId, IReadOnlyCollection<string> encounterIds, CancellationToken cancellationToken = default);
    Task<List<EncounterMappingModel>> GetByFacilityIdAndPatientIdAsync(string facilityId, string patientId, CancellationToken cancellationToken = default);
    Task<PagedConfigModel<EncounterMappingModel>> SearchAsync(
        EncounterMappingSearchModel search,
        int pageNumber,
        int pageSize,
        string? sortBy = null,
        SortOrder? sortOrder = null);
}

public class EncounterMappingQueries : IEncounterMappingQueries
{
    private readonly IDatabase _database;

    public EncounterMappingQueries(IDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<EncounterMappingModel?> GetByIdAsync(int id)
    {
        var entity = await _database.EncounterMappingRepository.GetAsync(id);
        if (entity == null)
            return null;

        var locations = await LoadEncounterLocationsAsync([entity.EncounterMappingId]);
        return ProjectToModel(entity, locations);
    }

    public async Task<List<EncounterMappingModel>> GetByFacilityIdAsync(string facilityId)
    {
        var entities = await _database.EncounterMappingRepository.FindAsync(m => m.FacilityId == facilityId);
        var locations = await LoadEncounterLocationsAsync(entities.Select(m => m.EncounterMappingId));
        return entities.Select(entity => ProjectToModel(entity, locations)).ToList();
    }

    public async Task<List<EncounterMappingModel>> GetByPatientIdAsync(string patientId)
    {
        var entities = await _database.EncounterMappingRepository.FindAsync(m => m.PatientId == patientId);
        var locations = await LoadEncounterLocationsAsync(entities.Select(m => m.EncounterMappingId));
        return entities.Select(entity => ProjectToModel(entity, locations)).ToList();
    }

    public async Task<EncounterMappingModel?> GetByEncounterIdAsync(string encounterId)
    {
        var entity = await _database.EncounterMappingRepository.FirstOrDefaultAsync(m => m.EncounterId == encounterId);
        if (entity == null)
            return null;

        var locations = await LoadEncounterLocationsAsync([entity.EncounterMappingId]);
        return ProjectToModel(entity, locations);
    }

    public async Task<EncounterMappingModel?> GetByFacilityIdAndEncounterIdAsync(string facilityId, string encounterId)
    {
        var entity = await _database.EncounterMappingRepository
            .FirstOrDefaultAsync(m => m.FacilityId == facilityId && m.EncounterId == encounterId);
        if (entity == null)
            return null;

        var locations = await LoadEncounterLocationsAsync([entity.EncounterMappingId]);
        return ProjectToModel(entity, locations);
    }

    public async Task<List<EncounterMappingModel>> GetByFacilityIdAndEncounterIdsAsync(string facilityId, IReadOnlyCollection<string> encounterIds, CancellationToken cancellationToken = default)
    {
        if (encounterIds.Count == 0)
        {
            return [];
        }

        var entities = await _database.EncounterMappingRepository
            .FindAsync(m => m.FacilityId == facilityId && encounterIds.Contains(m.EncounterId), cancellationToken);

        var locations = await LoadEncounterLocationsAsync(entities.Select(m => m.EncounterMappingId), cancellationToken);
        return entities.Select(entity => ProjectToModel(entity, locations)).ToList();
    }

    public async Task<List<EncounterMappingModel>> GetByFacilityIdAndPatientIdAsync(string facilityId, string patientId, CancellationToken cancellationToken = default)
    {
        var entities = await _database.EncounterMappingRepository
            .FindAsync(m => m.FacilityId == facilityId && m.PatientId == patientId, cancellationToken);
        var locations = await LoadEncounterLocationsAsync(entities.Select(m => m.EncounterMappingId), cancellationToken);
        return entities.Select(entity => ProjectToModel(entity, locations)).ToList();
    }

    public async Task<PagedConfigModel<EncounterMappingModel>> SearchAsync(
        EncounterMappingSearchModel search,
        int pageNumber,
        int pageSize,
        string? sortBy = null,
        SortOrder? sortOrder = null)
    {
        Expression<Func<EncounterMapping, bool>> filter = m =>
            (string.IsNullOrEmpty(search.FacilityId) || m.FacilityId == search.FacilityId) &&
            (string.IsNullOrEmpty(search.PatientId) || m.PatientId == search.PatientId) &&
            (string.IsNullOrEmpty(search.EncounterId) || m.EncounterId == search.EncounterId) &&
            (!search.MappedToOrg.HasValue || m.MappedToOrg == search.MappedToOrg.Value);

        var (records, metadata) = await _database.EncounterMappingRepository.SearchAsync(
            filter,
            string.IsNullOrWhiteSpace(sortBy) ? "CreateDate" : sortBy,
            sortOrder ?? SortOrder.Descending,
            pageSize,
            pageNumber);

        var locations = await LoadEncounterLocationsAsync(records.Select(m => m.EncounterMappingId));

        return new PagedConfigModel<EncounterMappingModel>
        {
            Records = records.Select(entity => ProjectToModel(entity, locations)).ToList(),
            Metadata = metadata
        };
    }

    // Loads the EncounterLocation rows for the given mappings and resolves each to its human-facing
    // LocationId by joining to OrganizationLocationMapping — done in two batched queries (not per-row)
    // to avoid N+1 access on the (non-lazy-loaded) EncounterLocation/OrganizationLocationMapping navigations.
    /// <remarks>
    /// The token is optional so the callers whose own signatures do not take one still compile, but the
    /// two that do must pass it: this sits on the acquisition-tail path reached from
    /// AcquisitionProcessorBackgroundService, where a shutdown would otherwise wait on two uncancellable
    /// round trips.
    /// </remarks>
    private async Task<Dictionary<int, List<EncounterLocationModel>>> LoadEncounterLocationsAsync(
        IEnumerable<int> encounterMappingIds,
        CancellationToken cancellationToken = default)
    {
        var mappingIds = encounterMappingIds.Distinct().ToList();
        if (mappingIds.Count == 0)
            return [];

        var encounterLocations = await _database.EncounterLocationRepository
            .FindAsync(l => mappingIds.Contains(l.EncounterMappingId), cancellationToken);

        var organizationLocationMappingIds = encounterLocations
            .Select(l => l.OrganizationLocationMappingId)
            .Distinct()
            .ToList();

        var organizationLocations = organizationLocationMappingIds.Count == 0
            ? []
            : await _database.LocationMappingRepository
                .FindAsync(l => organizationLocationMappingIds.Contains(l.LocationMappingId), cancellationToken);

        var organizationLocationsById = organizationLocations.ToDictionary(l => l.LocationMappingId);

        return encounterLocations
            .GroupBy(l => l.EncounterMappingId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(location => ProjectEncounterLocation(location, organizationLocationsById))
                    .ToList());
    }

    private static EncounterMappingModel ProjectToModel(
        EncounterMapping entity,
        IReadOnlyDictionary<int, List<EncounterLocationModel>> encounterLocationsByMappingId)
    {
        return new EncounterMappingModel
        {
            EncounterMappingId = entity.EncounterMappingId,
            FacilityId = entity.FacilityId,
            PatientId = entity.PatientId,
            EncounterId = entity.EncounterId,
            MappedToOrg = entity.MappedToOrg,
            CreateDate = entity.CreateDate,
            ModifiedDate = entity.ModifiedDate,
            EncounterLocations = encounterLocationsByMappingId.TryGetValue(entity.EncounterMappingId, out var locations)
                ? locations
                : []
        };
    }

    private static EncounterLocationModel ProjectEncounterLocation(
        EncounterLocation entity,
        IReadOnlyDictionary<int, OrganizationLocationMapping> organizationLocationsById)
    {
        var mapping = organizationLocationsById.TryGetValue(entity.OrganizationLocationMappingId, out var m)
            ? m
            : entity.OrganizationLocationMapping;
        
        return new EncounterLocationModel
        {
            EncounterLocationId = entity.EncounterLocationId,
            EncounterMappingId = entity.EncounterMappingId,
            OrganizationLocationMappingId = entity.OrganizationLocationMappingId,
            LocationId = mapping?.LocationId,
            LocationName = mapping?.LocationName,
            LocationAlias = mapping?.LocationAlias,
            PartOfValue = mapping?.PartOfValue,
            IsOrgLocation = mapping?.IsOrgLocation ?? false,
            CreateDate = entity.CreateDate,
            ModifiedDate = entity.ModifiedDate
        };
    }
}
