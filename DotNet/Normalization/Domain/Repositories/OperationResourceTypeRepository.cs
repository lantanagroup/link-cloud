using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;

namespace LantanaGroup.Link.Normalization.Domain.Repositories
{
    public class OperationResourceTypeRepository : EntityRepository<OperationResourceType, NormalizationDbContext>
    {
        public OperationResourceTypeRepository(NormalizationDbContext dbContext) : base(dbContext)
        {
        }
    }
}
