using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;

namespace LantanaGroup.Link.QueryDispatch.Application.Interfaces
{
    public interface IQueryDispatchConfigurationRepository : IPersistenceRepository<QueryDispatchConfigurationEntity>
    {
        Task<QueryDispatchConfigurationEntity> GetByFacilityId(string facilityId);
        Task<bool> DeleteByFacilityId(string facilityId);
        Task<List<QueryDispatchConfigurationEntity>> GetAllAsync();
        Task Update(QueryDispatchConfigurationEntity config);
    }
}
