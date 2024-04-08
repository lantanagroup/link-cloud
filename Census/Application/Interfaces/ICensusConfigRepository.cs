using Census.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;

namespace LantanaGroup.Link.Census.Application.Interfaces;

public interface ICensusConfigRepository : IPersistenceRepository<CensusConfigEntity>, IDisposable
{
    Task<IEnumerable<CensusConfigEntity>> GetAllFacilities(CancellationToken cancellationToken = default);
    Task<CensusConfigEntity> GetByFacilityIdAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<bool> RemoveByFacilityIdAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<bool> HealthCheck();
}
