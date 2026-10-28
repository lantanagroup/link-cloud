using LantanaGroup.Link.Shared.Application.Models.Tenant;

namespace LantanaGroup.Link.Shared.Application.Services;

public interface ITenantApiService
{
    Task<bool> CheckFacilityExists(string facilityId, CancellationToken cancellationToken = default);
    Task<FacilityModel> GetFacilityConfig(string facilityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the Key Vault secret name holding the signing key for the facility's vendor, or
    /// null when the facility has no vendor or that vendor has no key configured.
    /// </summary>
    Task<string?> GetVendorSigningKeySecretId(string facilityId, CancellationToken cancellationToken = default);
}
