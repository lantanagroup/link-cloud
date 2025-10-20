using LantanaGroup.Link.Shared.Application.Models.Tenant;

namespace LantanaGroup.Link.Shared.Application.Services;

public interface ITenantApiService
{
    Task<bool> CheckFacilityExists(string facilityId, CancellationToken cancellationToken = default);
    Task<FacilityModel> GetFacilityConfig(string facilityId, CancellationToken cancellationToken = default);
}
