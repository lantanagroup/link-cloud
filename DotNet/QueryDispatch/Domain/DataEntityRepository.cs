using LantanaGroup.Link.Shared.Domain.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using QueryDispatch.Domain.Context;

namespace QueryDispatch.Domain
{
    public class DataEntityRepository<T> : BaseEntityRepository<T> where T : BaseEntity
    {
        public DataEntityRepository(ILogger<BaseEntityRepository<T>> logger, QueryDispatchDbContext dbContext) : base(logger, dbContext)
        {

        }
    }
}
