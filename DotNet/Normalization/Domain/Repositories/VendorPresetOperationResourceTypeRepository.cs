using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;

namespace LantanaGroup.Link.Normalization.Domain.Repositories
{
    public class VendorPresetOperationResourceTypeRepository : EntityRepository<VendorPresetOperationResourceType, NormalizationDbContext>
    {
        public VendorPresetOperationResourceTypeRepository(NormalizationDbContext dbContext) : base(dbContext)
        {
        }
    }
}
