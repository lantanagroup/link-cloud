using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;

namespace LantanaGroup.Link.Normalization.Domain.Repositories
{
    public class ResourceTypeRepository : EntityRepository<ResourceType, NormalizationDbContext>
    {
        public ResourceTypeRepository(NormalizationDbContext dbContext) : base(dbContext)
        {
        }
    }
}
