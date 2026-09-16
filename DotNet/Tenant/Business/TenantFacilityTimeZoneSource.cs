using LantanaGroup.Link.DMRP.Business;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Tenant.Entities;

public class TenantFacilityTimeZoneSource : IFacilityTimeZoneSource
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