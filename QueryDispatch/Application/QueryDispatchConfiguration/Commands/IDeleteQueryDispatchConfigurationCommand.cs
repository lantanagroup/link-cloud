using LantanaGroup.Link.QueryDispatch.Domain.Entities;

namespace LantanaGroup.Link.QueryDispatch.Application.QueryDispatchConfiguration.Commands
{
    public interface IDeleteQueryDispatchConfigurationCommand
    {
        Task<bool> Execute(string facilityId);
    }
}
