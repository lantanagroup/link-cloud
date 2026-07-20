using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models;
using LantanaGroup.Link.Nhsn.App.Bff.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services;

public class FacilityAdministrationService : IFacilityAdministrationService
{
    private readonly NhsnAppDbContext _dbContext;

    public FacilityAdministrationService(NhsnAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<FacilitySummaryResponse?> UpdateFacilityOnboardingAsync(string facilityId, string actingFacilityId, bool isFacilityAdmin, UpdateFacilityOnboardingRequest request, CancellationToken cancellationToken = default)
    {
        if (!isFacilityAdmin)
        {
            throw new InvalidOperationException("FACADMIN is required to update facility onboarding.");
        }

        if (!string.Equals(facilityId, actingFacilityId, StringComparison.OrdinalIgnoreCase))
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