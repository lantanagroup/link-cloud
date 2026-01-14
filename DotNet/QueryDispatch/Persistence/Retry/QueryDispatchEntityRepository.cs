using LantanaGroup.Link.Shared.Domain.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using QueryDispatch.Domain.Context;

namespace QueryDispatch.Persistence.Retry;

public class QueryDispatchEntityRepository<T> : BaseEntityRepository<T, QueryDispatchDbContext> where T : BaseEntity
{
    public QueryDispatchEntityRepository(ILogger<QueryDispatchEntityRepository<T>> logger, QueryDispatchDbContext dbContext) : base(logger, dbContext)
    {

    }
}
