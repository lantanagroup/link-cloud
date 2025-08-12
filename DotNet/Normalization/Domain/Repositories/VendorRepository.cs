using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;

namespace LantanaGroup.Link.Normalization.Domain.Repositories
{
    public class VendorRepository : EntityRepository<Vendor, NormalizationDbContext>
    {
        public VendorRepository(NormalizationDbContext dbContext) : base(dbContext)
        {
        }
    }
}
