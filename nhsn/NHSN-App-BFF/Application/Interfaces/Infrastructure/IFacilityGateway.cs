using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;

/// <summary>
/// The Tenant service, in our vocabulary. Reference port for the gateway pattern — no Link type
/// crosses this boundary, so the Application layer never sees <c>FacilityModel</c> or
/// <c>LinkApiResponse</c>.
/// </summary>
public interface IFacilityGateway
{
    /// <summary>Reads the facility, or null when Tenant has no record for it yet.</summary>
    Task<FacilityInfo?> GetAsync(string facilityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the facility as a complete object: reads current state, overlays these values,
    /// creates if absent and updates if present. Idempotent, so a failed commit can safely re-run.
    /// </summary>
    /// <remarks>
    /// Covers the stale-client hazard — <see cref="FacilityInfo"/> carries only the four values this
    /// step owns, so a caller can't overwrite another step's section. Does not cover interleaving:
    /// two callers that both GET before either PUTs will silently drop one section. A second caller
    /// needs a per-facility write lock around both calls.
    /// </remarks>
    Task SaveAsync(FacilityInfo facilityInfo, CancellationToken cancellationToken = default);
}
