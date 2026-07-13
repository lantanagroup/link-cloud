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

    public async Task<IReadOnlyCollection<FacilitySummaryResponse>> GetFacilitiesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Facilities
            .AsNoTracking()
            .OrderBy(x => x.FacilityId)
            .Select(x => new FacilitySummaryResponse
            {
                Id = x.Id,
                FacilityId = x.FacilityId,
                IsOnboarded = x.IsOnboarded
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<FacilitySummaryResponse?> UpdateFacilityOnboardingAsync(string facilityId, UpdateFacilityOnboardingRequest request, CancellationToken cancellationToken = default)
    {
        var facility = await _dbContext.Facilities.SingleOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);
        if (facility is null)
        {
            return null;
        }

        facility.IsOnboarded = request.IsOnboarded;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _dbContext.Users
            .Where(x => x.FacilityId == facilityId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.IsOnboarded, request.IsOnboarded), cancellationToken);

        return new FacilitySummaryResponse
        {
            Id = facility.Id,
            FacilityId = facility.FacilityId,
            IsOnboarded = facility.IsOnboarded
        };
    }
}