using LantanaGroup.Link.Census.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;

namespace LantanaGroup.Link.Census.Application.Interfaces;

public interface ICensusPatientListRepository : IEntityRepository<CensusPatientListEntity>
{
    Task<List<CensusPatientListEntity>> GetActivePatientsForFacility(string facilityId, CancellationToken cancellationToken = default);
    Task<List<CensusPatientListEntity>> GetAllPatientsForFacility(string facilityId, DateTime startDate = default, DateTime endDate = default, CancellationToken cancellationToken = default);
    Task<CensusPatientListEntity> GetPatientByPatientId(string facilityId, string patientId, CancellationToken cancellationToken = default);
}
