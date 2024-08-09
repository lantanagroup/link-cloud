using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using LantanaGroup.Link.Shared.Domain.Entities;
using QueryDispatch.Domain.Context;

namespace QueryDispatch.Domain
{
    public class DataEntityRepository<T> : EntityRepository<T> where T : BaseEntity
    {
        public DataEntityRepository(ILogger<EntityRepository<T>> logger, QueryDispatchDbContext dbContext) : base(logger, dbContext)
        {

        }
    }
}
