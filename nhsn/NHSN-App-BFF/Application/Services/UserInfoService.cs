using System.Security.Claims;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;
using LantanaGroup.Link.Nhsn.App.Bff.Persistence;
using LantanaGroup.Link.Nhsn.App.Bff.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services;

public class UserInfoService : IUserInfoService
{
    private const string AccessStateAllowed = "Allowed";
    private const string AccessStateMissingFacility = "MissingFacility";
    private const string AccessStateMissingRequiredRole = "MissingRequiredRole";

    private readonly NhsnAppDbContext _dbContext;
    private readonly NhsnJwtSettings _jwtSettings;

    public UserInfoService(NhsnAppDbContext dbContext, IOptions<NhsnJwtSettings> jwtOptions)
    {
        _dbContext = dbContext;
        _jwtSettings = jwtOptions.Value;
    }

    public async Task<UserInfoResponse> GetUserInfoAsync(ClaimsPrincipal principal, HttpRequest request, CancellationToken cancellationToken = default)
    {
        var incomingUser = ResolveIncomingUser(principal);
        NhsnFacility? facility = null;

        if (!string.IsNullOrWhiteSpace(incomingUser.FacilityId))
        {
            facility = await _dbContext.Facilities.SingleOrDefaultAsync(x => x.FacilityId == incomingUser.FacilityId, cancellationToken);
            if (facility is null)
            {
                facility = new NhsnFacility
                {
                    FacilityId = incomingUser.FacilityId,
                    IsOnboarded = false
                };

                _dbContext.Facilities.Add(facility);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        var user = await _dbContext.Users.SingleOrDefaultAsync(x => x.ExternalUserId == incomingUser.ExternalUserId, cancellationToken);

        if (user is null)
        {
            user = new NhsnUser
            {
                ExternalUserId = incomingUser.ExternalUserId,
                Email = incomingUser.Email,
                Name = incomingUser.Name,
                GroupsRaw = string.Join(',', incomingUser.Groups),
                FacilityId = incomingUser.FacilityId,
                CreatedBy = "userinfo",
                LastModifiedBy = "userinfo",
                LastAccessedOn = DateTime.UtcNow
            };

            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            user.Email = incomingUser.Email;
            user.Name = incomingUser.Name;
            user.GroupsRaw = string.Join(',', incomingUser.Groups);
            user.FacilityId = incomingUser.FacilityId;
            user.LastModifiedBy = "userinfo";
            user.LastModifiedOn = DateTime.UtcNow;
            user.LastAccessedOn = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var effectiveGroups = BuildEffectiveGroups(incomingUser.Groups);
        var hasFacility = !string.IsNullOrWhiteSpace(user.FacilityId);
        var isFacilityAdmin = effectiveGroups.Contains("FACADMIN", StringComparer.OrdinalIgnoreCase);
        var facilityIsOnboarded = facility?.IsOnboarded ?? false;
        var accessState = !hasFacility
            ? AccessStateMissingFacility
            : !isFacilityAdmin
                ? AccessStateMissingRequiredRole
                : AccessStateAllowed;
        var availableNavigation = accessState != AccessStateAllowed
            ? Array.Empty<string>()
            : facilityIsOnboarded
                ? new[] { "configuration" }
                : new[] { "onboarding" };

        return new UserInfoResponse
        {
            AccessState = accessState,
            Email = user.Email,
            Name = user.Name,
            IsFacilityAdmin = isFacilityAdmin,
            IsOnboarded = facilityIsOnboarded,
            HasFacility = hasFacility,
            FacilityId = user.FacilityId,
            Groups = effectiveGroups,
            AvailableNavigation = availableNavigation,
            AccessRequestUrl = _jwtSettings.AccessRequestUrl
        };
    }

    private IncomingUser ResolveIncomingUser(ClaimsPrincipal principal)
    {
        var externalUserId = principal.FindFirstValue(_jwtSettings.UserIdClaimType)
                             ?? principal.FindFirstValue(_jwtSettings.EmailClaimType)
                             ?? principal.Identity?.Name
                             ?? throw new InvalidOperationException("Unable to resolve user identifier from the authenticated principal.");

        var email = principal.FindFirstValue(_jwtSettings.EmailClaimType)
                    ?? principal.FindFirstValue(ClaimTypes.Email)
                    ?? externalUserId;

        var name = principal.FindFirstValue(_jwtSettings.NameDisplayClaimType)
                   ?? principal.FindFirstValue(_jwtSettings.NameClaimType)
                   ?? principal.Identity?.Name
                   ?? email;

        var groups = principal.FindAll(_jwtSettings.GroupsClaimType).Select(x => x.Value).ToArray();
        var facilityId = principal.FindFirstValue(_jwtSettings.FacilityIdClaimType);

        return new IncomingUser(externalUserId, email, name, groups, facilityId);
    }

    private static string[] BuildEffectiveGroups(string[] incomingGroups)
    {
        return incomingGroups
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private sealed record IncomingUser(string ExternalUserId, string Email, string Name, string[] Groups, string? FacilityId);
}
