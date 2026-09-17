using LantanaGroup.Link.DMRP.Business;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Tenant.Entities;

namespace LantanaGroup.Link.Tenant.Business
{
    /// <summary>
    /// Reads a facility's timezone from the Tenant service's own database, which is where the host
    /// persists facilities. The module needs to know the timezone of a facility to resolve its
    /// reporting period, which is the month the facility is in by its own timezone, not UTC.
    /// </summary>
    public sealed class TenantFacilityTimeZoneSource : IFacilityTimeZoneSource
    {
        private readonly IEntityRepository<Facility> _facilityRepository;

        public TenantFacilityTimeZoneSource(IEntityRepository<Facility> facilityRepository)
        {
            _facilityRepository = facilityRepository ?? throw new ArgumentNullException(nameof(facilityRepository));
        }

        public async Task<string?> GetTimeZoneAsync(string facilityId, CancellationToken cancellationToken = default)
        {
            var facility = await _facilityRepository.FirstOrDefaultAsync(f => f.FacilityId == facilityId, cancellationToken);
            
            return facility?.TimeZone;
        }
    }
}