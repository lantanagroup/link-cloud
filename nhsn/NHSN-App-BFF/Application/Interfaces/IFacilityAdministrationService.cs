using LantanaGroup.Link.Nhsn.App.Bff.Application.Models;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces;

public interface IFacilityAdministrationService
{
    Task<IReadOnlyCollection<FacilitySummaryResponse>> GetFacilitiesAsync(CancellationToken cancellationToken = default);
    Task<FacilitySummaryResponse?> UpdateFacilityOnboardingAsync(string facilityId, UpdateFacilityOnboardingRequest request, CancellationToken cancellationToken = default);
}