using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.FacilityAdministration;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Enums;
using LantanaGroup.Link.Nhsn.App.Bff.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.FacilityAdministration;

public class FacilityAdministrationService : IFacilityAdministrationService
{
    private readonly NhsnAppDbContext _dbContext;
    private readonly INhsnUserContext _userContext;
    private readonly IFacilityWriteLock _writeLock;

    public FacilityAdministrationService(NhsnAppDbContext dbContext, INhsnUserContext userContext, IFacilityWriteLock writeLock)
    {
        _dbContext = dbContext;
        _userContext = userContext;
        _writeLock = writeLock;
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

        // Shares the lock OnboardingWriteService uses — this writes the same row and must not
        // interleave with a step save.
        await using var writeLock = await _writeLock.AcquireAsync(facilityId, cancellationToken);

        var facility = await _dbContext.Facilities.SingleOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);
        if (facility is null)
        {
            return null;
        }

        ApplyOnboardingFlag(facility, request.IsOnboarded);
        facility.LastModifiedOn = DateTime.UtcNow;
        facility.LastModifiedBy = _userContext.ExternalUserId;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await writeLock.CommitAsync(cancellationToken);

        return new FacilitySummaryResponse
        {
            Id = facility.Id,
            FacilityId = facility.FacilityId,
            IsOnboarded = facility.IsOnboarded
        };
    }

    // Translates the legacy boolean into a status transition. OnboardingStatus is the system of
    // record; IsOnboarded is derived from it and never stored. This route predates the status and
    // keeps its shape for the existing contract, but its handler moves the status instead.
    //
    // false deliberately does not reset to NotStarted. Un-completing a facility means "not finished
    // after all", not "discard the journey" — the draft, commit ledger and completed steps survive.
    // A facility that was never complete is left alone entirely.
    private static void ApplyOnboardingFlag(NhsnFacility facility, bool isOnboarded)
    {
        if (isOnboarded)
        {
            facility.OnboardingStatus = OnboardingStatus.Complete;
            facility.CompletedOn ??= DateTime.UtcNow;
            return;
        }

        if (facility.OnboardingStatus == OnboardingStatus.Complete)
        {
            facility.OnboardingStatus = OnboardingStatus.InProgress;
            facility.CompletedOn = null;
        }
    }
}
