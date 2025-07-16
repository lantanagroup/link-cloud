using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;

namespace LantanaGroup.Link.Normalization.Domain.Repositories
{
    public class VendorVersionOperationPresetRepository : EntityRepository<VendorVersionOperationPreset, NormalizationDbContext>
    {
        public VendorVersionOperationPresetRepository(NormalizationDbContext dbContext) : base(dbContext)
        {
        }
    }
}
