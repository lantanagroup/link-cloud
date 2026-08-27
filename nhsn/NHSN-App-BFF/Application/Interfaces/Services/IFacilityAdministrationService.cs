using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.FacilityAdministration;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

public interface IFacilityAdministrationService
{
    /// <summary>
    /// Updates the onboarding flag for <paramref name="facilityId"/>.
    /// </summary>
    /// <remarks>
    /// Takes no acting facility or role argument: both come from <see cref="INhsnUserContext"/>.
    /// The route keeps its facility segment for compatibility, validated against the claim.
    /// </remarks>
    Task<FacilitySummaryResponse?> UpdateFacilityOnboardingAsync(string facilityId, UpdateFacilityOnboardingRequest request, CancellationToken cancellationToken = default);
}
