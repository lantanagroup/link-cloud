namespace LantanaGroup.Link.QueryDispatch.Application.Services;

public interface ITenantApiService
{
    Task<bool> CheckFacilityExists(string facilityId, CancellationToken cancellationToken = default);
}
