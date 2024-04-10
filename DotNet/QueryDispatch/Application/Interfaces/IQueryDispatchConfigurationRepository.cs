using LantanaGroup.Link.QueryDispatch.Domain.Entities;

namespace LantanaGroup.Link.QueryDispatch.Application.Interfaces
{
    public interface IQueryDispatchConfigurationRepository : IBaseRepository<QueryDispatchConfigurationEntity>
    {
        Task<QueryDispatchConfigurationEntity> GetByFacilityId(string facilityId);
        Task<bool> DeleteByFacilityId(string facilityId);
        Task Update(QueryDispatchConfigurationEntity config);
    }
}
