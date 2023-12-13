using LantanaGroup.Link.QueryDispatch.Domain.Entities;

namespace LantanaGroup.Link.QueryDispatch.Application.Interfaces
{
    public interface IQueryDispatchConfigurationFactory
    {
        QueryDispatchConfigurationEntity CreateQueryDispatchConfiguration(string facilityId, List<DispatchSchedule> dispatchSchedules);
    }
}

