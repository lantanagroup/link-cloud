using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Normalization.Domain.Repositories
{
    public class ResourceTypeRepository : EntityRepository<ResourceType, NormalizationDbContext>
    {
        public ResourceTypeRepository(DbContext dbContext) : base(dbContext)
        {
        }
    }
}
