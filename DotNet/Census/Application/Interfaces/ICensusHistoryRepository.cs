using LantanaGroup.Link.Census.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;

namespace LantanaGroup.Link.Census.Application.Interfaces;

public interface ICensusHistoryRepository : IEntityRepository<PatientCensusHistoricEntity>
{
    Task<IEnumerable<PatientCensusHistoricEntity>> GetAllCensusReportsForFacility(string facilityId);
}
