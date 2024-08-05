using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using QueryDispatch.Domain.Context;

namespace LantanaGroup.Link.QueryDispatch.Persistence.QueryDispatchConfiguration
{
    public class QueryDispatchConfigurationRepo : EntityRepository<QueryDispatchConfigurationEntity>, IQueryDispatchConfigurationRepository
    {
        public QueryDispatchConfigurationRepo(ILogger<QueryDispatchConfigurationRepo> logger, QueryDispatchDbContext dbContext) : base(logger, dbContext) { }
    }
}
