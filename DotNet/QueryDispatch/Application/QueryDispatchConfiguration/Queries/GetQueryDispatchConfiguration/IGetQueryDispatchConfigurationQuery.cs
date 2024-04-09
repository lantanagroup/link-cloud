using LantanaGroup.Link.QueryDispatch.Domain.Entities;

namespace LantanaGroup.Link.QueryDispatch.Application.Queries
{
    public interface IGetQueryDispatchConfigurationQuery
    {
        Task<QueryDispatchConfigurationEntity> Execute(string facilityId);
    }
}
