using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;

namespace LantanaGroup.Link.Normalization.Domain.Repositories
{
    public class VendorVersionRepository : EntityRepository<VendorVersion, NormalizationDbContext>
    {
        public VendorVersionRepository(NormalizationDbContext dbContext) : base(dbContext)
        {
        }
    }
}
