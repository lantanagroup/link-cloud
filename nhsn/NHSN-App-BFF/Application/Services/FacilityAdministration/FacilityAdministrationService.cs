using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.FacilityAdministration;
using LantanaGroup.Link.Nhsn.App.Bff.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.FacilityAdministration;

public class FacilityAdministrationService : IFacilityAdministrationService
{
    private readonly NhsnAppDbContext _dbContext;
    private readonly INhsnUserContext _userContext;

    public FacilityAdministrationService(NhsnAppDbContext dbContext, INhsnUserContext userContext)
    {
        _dbContext = dbContext;
        _userContext = userContext;
    }

    public async Task<FacilitySummaryResponse?> UpdateFacilityOnboardingAsync(string facilityId, UpdateFacilityOnboardingRequest request, CancellationToken cancellationToken = default)
    {
        // Both checks read the token, not a persisted row.
        if (!_userContext.IsFacilityAdmin)
        {
            throw new InvalidOperationException("FACADMIN is required to update facility onboarding.");
        }

        if (!string.Equals(facilityId, _userContext.RequireFacilityId(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Facility onboarding may only be updated for the authenticated facility context.");
        }

        var facility = await _dbContext.Facilities.SingleOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);
        if (facility is null)
        {
            return null;
        }

        facility.IsOnboarded = request.IsOnboarded;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new FacilitySummaryResponse
        {
            Id = facility.Id,
            FacilityId = facility.FacilityId,
            IsOnboarded = facility.IsOnboarded
        };
    }
}
