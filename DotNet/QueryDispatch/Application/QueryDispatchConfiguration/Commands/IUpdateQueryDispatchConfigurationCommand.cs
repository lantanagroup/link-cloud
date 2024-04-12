using LantanaGroup.Link.QueryDispatch.Domain.Entities;

namespace LantanaGroup.Link.QueryDispatch.Application.QueryDispatchConfiguration.Commands
{
    public interface IUpdateQueryDispatchConfigurationCommand
    {
        Task Execute(QueryDispatchConfigurationEntity existingConfig, List<DispatchSchedule> dispatchSchedules);
    }
}
