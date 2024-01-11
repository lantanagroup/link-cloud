namespace LantanaGroup.Link.Normalization.Application.Services;

public interface ITenantApiService
{
    Task<bool> CheckFacilityExists(string facilityId, CancellationToken cancellationToken = default);
}
