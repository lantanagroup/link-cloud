using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;

namespace LantanaGroup.Link.QueryDispatch.Application.Factory
{
    public class QueryDispatchConfigurationFactory : IQueryDispatchConfigurationFactory
    {
        public QueryDispatchConfigurationEntity CreateQueryDispatchConfiguration(string facilityId, List<DispatchSchedule> dispatchSchedules)
        {
            return new QueryDispatchConfigurationEntity()
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = facilityId,
                DispatchSchedules = dispatchSchedules,
                CreateDate = DateTime.UtcNow
            };
        }
    }
}
