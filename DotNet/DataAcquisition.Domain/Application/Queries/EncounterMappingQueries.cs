using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
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
    Task<List<EncounterMappingModel>> GetByFacilityIdAndPatientIdAsync(string facilityId, string patientId);
    Task<PagedConfigModel<EncounterMappingModel>> SearchAsync(EncounterMappingSearchModel search, int pageNumber, int pageSize);
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
        return entity != null ? ProjectToModel(entity) : null;
    }

    public async Task<List<EncounterMappingModel>> GetByFacilityIdAsync(string facilityId)
    {
        var entities = await _database.EncounterMappingRepository.FindAsync(m => m.FacilityId == facilityId);
        return entities.Select(ProjectToModel).ToList();
    }

    public async Task<List<EncounterMappingModel>> GetByPatientIdAsync(string patientId)
    {
        var entities = await _database.EncounterMappingRepository.FindAsync(m => m.PatientId == patientId);
        return entities.Select(ProjectToModel).ToList();
    }

    public async Task<EncounterMappingModel?> GetByEncounterIdAsync(string encounterId)
    {
        var entity = await _database.EncounterMappingRepository.FirstOrDefaultAsync(m => m.EncounterId == encounterId);
        return entity != null ? ProjectToModel(entity) : null;
    }

    public async Task<EncounterMappingModel?> GetByFacilityIdAndEncounterIdAsync(string facilityId, string encounterId)
    {
        var entity = await _database.EncounterMappingRepository
            .FirstOrDefaultAsync(m => m.FacilityId == facilityId && m.EncounterId == encounterId);
        return entity != null ? ProjectToModel(entity) : null;
    }

    public async Task<List<EncounterMappingModel>> GetByFacilityIdAndEncounterIdsAsync(string facilityId, IReadOnlyCollection<string> encounterIds, CancellationToken cancellationToken = default)
    {
        if (encounterIds.Count == 0)
        {
            return [];
        }

        var entities = await _database.EncounterMappingRepository
            .FindAsync(m => m.FacilityId == facilityId && encounterIds.Contains(m.EncounterId), cancellationToken);

        return entities.Select(ProjectToModel).ToList();
    }

    public async Task<List<EncounterMappingModel>> GetByFacilityIdAndPatientIdAsync(string facilityId, string patientId)
    {
        var entities = await _database.EncounterMappingRepository
            .FindAsync(m => m.FacilityId == facilityId && m.PatientId == patientId);
        return entities.Select(ProjectToModel).ToList();
    }

    public async Task<PagedConfigModel<EncounterMappingModel>> SearchAsync(EncounterMappingSearchModel search, int pageNumber, int pageSize)
    {
        Expression<Func<EncounterMapping, bool>> filter = m =>
            (string.IsNullOrEmpty(search.FacilityId) || m.FacilityId == search.FacilityId) &&
            (string.IsNullOrEmpty(search.PatientId) || m.PatientId == search.PatientId) &&
            (string.IsNullOrEmpty(search.EncounterId) || m.EncounterId == search.EncounterId) &&
            (!search.MappedToOrg.HasValue || m.MappedToOrg == search.MappedToOrg.Value);

        var (records, metadata) = await _database.EncounterMappingRepository.SearchAsync(filter, "CreateDate", LantanaGroup.Link.Shared.Application.Enums.SortOrder.Descending, pageSize, pageNumber);

        return new PagedConfigModel<EncounterMappingModel>
        {
            Records = records.Select(ProjectToModel).ToList(),
            Metadata = metadata
        };
    }

    private static EncounterMappingModel ProjectToModel(EncounterMapping entity)
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
            EncounterLocations = entity.EncounterLocations?.Select(l => new EncounterLocationModel
            {
                EncounterLocationId = l.EncounterLocationId,
                EncounterMappingId = l.EncounterMappingId,
                OrganizationLocationMappingId = l.OrganizationLocationMappingId,
                CreateDate = l.CreateDate,
                ModifiedDate = l.ModifiedDate
            }).ToList() ?? new List<EncounterLocationModel>()
        };
    }
}
