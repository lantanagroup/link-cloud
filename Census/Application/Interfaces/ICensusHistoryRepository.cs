using LantanaGroup.Link.Census.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;

namespace LantanaGroup.Link.Census.Application.Interfaces;

public interface ICensusHistoryRepository : IMongoDbRepository<PatientCensusHistoricEntity>
{
    List<PatientCensusHistoricEntity> GetAllCensusReportsForFacility(string facilityId);
}
