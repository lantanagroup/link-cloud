using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using LantanaGroup.Link.Shared.Domain.Entities;
using QueryDispatch.Domain.Context;

namespace QueryDispatch.Persistence.Retry;

public class QueryDispatchEntityRepository<T> : EntityRepository<T> where T : BaseEntity
{
    public QueryDispatchEntityRepository(ILogger<QueryDispatchEntityRepository<T>> logger, QueryDispatchDbContext dbContext) : base(logger, dbContext)
    {

    }
}
