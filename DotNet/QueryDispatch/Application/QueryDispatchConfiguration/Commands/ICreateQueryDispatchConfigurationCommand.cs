using LantanaGroup.Link.QueryDispatch.Domain.Entities;

namespace LantanaGroup.Link.QueryDispatch.Application.QueryDispatchConfiguration.Commands
{
    public interface ICreateQueryDispatchConfigurationCommand
    {
        Task Execute(QueryDispatchConfigurationEntity config);
    }
}
