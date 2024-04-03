using LantanaGroup.Link.Census.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;

namespace LantanaGroup.Link.Census.Application.Interfaces;

public interface ICensusPatientListRepository : ISqlPersistenceRepository<CensusPatientListEntity>
{
    Task<List<CensusPatientListEntity>> GetActivePatientsForFacility(string facilityId, CancellationToken cancellationToken = default);
    Task<List<CensusPatientListEntity>> GetAllPatientsForFacility(string facilityId, CancellationToken cancellationToken = default);
}
