using LantanaGroup.Link.DMRP.Business;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Tenant.Entities;

namespace LantanaGroup.Link.Tenant.Business
{
    public sealed class TenantFacilityExistence : IFacilityExistence
    {
        private readonly IEntityRepository<Facility> _facilities;

        public TenantFacilityExistence(IEntityRepository<Facility> facilities)
        {
            _facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
        }

        public Task<bool> ExistsAsync(string facilityId, CancellationToken cancellationToken = default) =>
            _facilities.AnyAsync(f => f.FacilityId == facilityId && !f.IsDeleted, cancellationToken);
    }
}
