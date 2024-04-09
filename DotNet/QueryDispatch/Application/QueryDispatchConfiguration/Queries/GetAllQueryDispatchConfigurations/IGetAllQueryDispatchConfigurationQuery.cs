using LantanaGroup.Link.QueryDispatch.Domain.Entities;

namespace LantanaGroup.Link.QueryDispatch.Application.Queries
{
    public interface IGetAllQueryDispatchConfigurationQuery
    {
        Task<List<QueryDispatchConfigurationEntity>> Execute();
    }
}
