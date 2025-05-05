using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Normalization.Domain.Repositories
{
    public class OperationResourceTypeRepository : EntityRepository<OperationResourceType, NormalizationDbContext>
    {
        public OperationResourceTypeRepository(DbContext dbContext) : base(dbContext)
        {
        }
    }
}
