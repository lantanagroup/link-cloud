using LantanaGroup.Link.Nhsn.App.Bff.Application.Models;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces;

public interface IFacilityAdministrationService
{
    Task<FacilitySummaryResponse?> UpdateFacilityOnboardingAsync(string facilityId, string actingFacilityId, bool isFacilityAdmin, UpdateFacilityOnboardingRequest request, CancellationToken cancellationToken = default);
}