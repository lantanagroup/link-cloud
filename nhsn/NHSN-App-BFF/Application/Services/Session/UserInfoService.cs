using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Session;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Enums;
using LantanaGroup.Link.Nhsn.App.Bff.Persistence;
using LantanaGroup.Link.Nhsn.App.Bff.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Session;

public class UserInfoService : IUserInfoService
{
    private const string UserInfoActor = "userinfo";

    private readonly NhsnAppDbContext _dbContext;
    private readonly INhsnUserContext _userContext;
    private readonly NhsnJwtSettings _jwtSettings;
    private readonly LinkCapabilitiesSettings _capabilities;

    public UserInfoService(
        NhsnAppDbContext dbContext,
        INhsnUserContext userContext,
        IOptions<NhsnJwtSettings> jwtOptions,
        IOptions<LinkCapabilitiesSettings> capabilityOptions)
    {
        _dbContext = dbContext;
        _userContext = userContext;
        _jwtSettings = jwtOptions.Value;
        _capabilities = capabilityOptions.Value;
    }

    public async Task<UserInfoResponse> GetUserInfoAsync(CancellationToken cancellationToken = default)
    {
        var facility = await EnsureFacilityAsync(cancellationToken);
        await UpsertUserAsync(cancellationToken);

        // Every authorization value below comes from _userContext, never from the rows touched above.
        var accessState = _userContext.AccessState;
        var facilityIsOnboarded = facility?.IsOnboarded ?? false;

        var availableNavigation = accessState != NhsnAccessState.Allowed
            ? Array.Empty<string>()
            : facilityIsOnboarded
                ? ["configuration"]
                : new[] { "onboarding" };

        return new UserInfoResponse
        {
            AccessState = accessState.ToString(),
            Email = _userContext.Email,
            Name = _userContext.Name,
            IsFacilityAdmin = _userContext.IsFacilityAdmin,
            IsOnboarded = facilityIsOnboarded,
            HasFacility = _userContext.HasFacility,
            FacilityId = _userContext.FacilityId,
            FacilityName = _userContext.FacilityName,
            Groups = _userContext.Groups,
            AvailableNavigation = availableNavigation,
            AccessRequestUrl = _jwtSettings.AccessRequestUrl,
            Vendor = facility?.Vendor?.ToString(),
            OnboardingStatus = facility?.OnboardingStatus.ToString(),
            CurrentStepId = facility?.CurrentStepId,
            Capabilities = new CapabilitiesResponse
            {
                FhirConnectionProbe = _capabilities.FhirConnectionProbe,
                PatientListWithNames = _capabilities.PatientListWithNames,
                SftpFileListing = _capabilities.SftpFileListing
            }
        };
    }

    // Provisions the facility row on first sight of a facility claim — currently the only path
    // that creates one, which is why a GET writes.
    private async Task<NhsnFacility?> EnsureFacilityAsync(CancellationToken cancellationToken)
    {
        var facilityId = _userContext.FacilityId;
        if (facilityId is null)
        {
            return null;
        }

        var facility = await _dbContext.Facilities.SingleOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);
        if (facility is not null)
        {
            return facility;
        }

        facility = new NhsnFacility
        {
            FacilityId = facilityId,
            OnboardingStatus = OnboardingStatus.NotStarted,
            CreatedBy = UserInfoActor,
            LastModifiedBy = UserInfoActor
        };

        _dbContext.Facilities.Add(facility);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return facility;
    }

    // Keeps a user row for attribution — acknowledgements and commits need a stable id, and a name
    // to render long after the person has left. Writes identity only: it must never gain a groups
    // or facility column again, since those are authorization state this row must not cache.
    private async Task UpsertUserAsync(CancellationToken cancellationToken)
    {
        var externalUserId = _userContext.ExternalUserId;
        var user = await _dbContext.Users.SingleOrDefaultAsync(x => x.ExternalUserId == externalUserId, cancellationToken);
        var now = DateTime.UtcNow;

        if (user is null)
        {
            _dbContext.Users.Add(new NhsnUser
            {
                ExternalUserId = externalUserId,
                Email = _userContext.Email,
                Name = _userContext.Name,
                CreatedBy = UserInfoActor,
                LastModifiedBy = UserInfoActor,
                LastAccessedOn = now
            });
        }
        else
        {
            user.Email = _userContext.Email;
            user.Name = _userContext.Name;
            user.LastModifiedBy = UserInfoActor;
            user.LastModifiedOn = now;
            user.LastAccessedOn = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
