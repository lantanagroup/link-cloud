using System.Security.Claims;
using System.Text.Json;
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
    private readonly NhsnAppDbContext _dbContext;
    private readonly NhsnJwtSettings _jwtSettings;

    public UserInfoService(NhsnAppDbContext dbContext, IOptions<NhsnJwtSettings> jwtOptions)
    {
        _dbContext = dbContext;
        _jwtSettings = jwtOptions.Value;
    }

    public async Task<UserInfoResponse> GetUserInfoAsync(ClaimsPrincipal principal, HttpRequest request, CancellationToken cancellationToken = default)
    {
        var incomingUser = ResolveIncomingUser(principal, request);
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
                IsOnboarded = facility?.IsOnboarded ?? false,
                IsActive = true
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
            user.IsOnboarded = facility?.IsOnboarded ?? false;
            user.LastModifiedBy = "userinfo";
            user.LastModifiedOn = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var effectiveGroups = BuildEffectiveGroups(incomingUser.Groups, user.IsAdmin);
        var isSystemAdmin = effectiveGroups.Contains(NhsnAppConstants.Roles.NhsnLinkSysAdmin, StringComparer.OrdinalIgnoreCase);
        var hasFacility = !string.IsNullOrWhiteSpace(user.FacilityId);
        var isFacilityAdmin = effectiveGroups.Contains("FACADMIN", StringComparer.OrdinalIgnoreCase);
        var facilityIsOnboarded = facility?.IsOnboarded ?? false;
        var availableNavigation = isSystemAdmin
            ? new[] { "users", "facilities" }
            : !hasFacility
                ? Array.Empty<string>()
                : isFacilityAdmin && !facilityIsOnboarded
                    ? new[] { "onboarding" }
                    : isFacilityAdmin && facilityIsOnboarded
                        ? new[] { "configuration" }
                        : new[] { "maintenance", "configuration-overview", "configuration-changes" };

        return new UserInfoResponse
        {
            Email = user.Email,
            Name = user.Name,
            Roles = effectiveGroups,
            IsSystemAdmin = isSystemAdmin,
            IsOnboarded = facilityIsOnboarded,
            IsActive = user.IsActive,
            IsAdmin = user.IsAdmin,
            HasFacility = hasFacility,
            FacilityId = user.FacilityId,
            Groups = effectiveGroups,
            AvailableNavigation = availableNavigation,
            AccessRequestUrl = _jwtSettings.AccessRequestUrl
        };
    }

    private IncomingUser ResolveIncomingUser(ClaimsPrincipal principal, HttpRequest request)
    {
        if (_jwtSettings.AllowSimulatedJwtHeader && request.Headers.TryGetValue(_jwtSettings.SimulatedJwtHeaderName, out var headerValue))
        {
            var payload = JsonSerializer.Deserialize<SimulatedUserHeaderPayload>(headerValue.ToString(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (payload is not null && !string.IsNullOrWhiteSpace(payload.Email))
            {
                return new IncomingUser(
                    payload.ExternalUserId ?? payload.Email,
                    payload.Email,
                    payload.Name,
                    payload.Groups,
                    payload.FacilityId);
            }
        }

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

    private static string[] BuildEffectiveGroups(string[] incomingGroups, bool isAdmin)
    {
        var groups = incomingGroups
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (isAdmin && !groups.Contains(NhsnAppConstants.Roles.NhsnLinkSysAdmin, StringComparer.OrdinalIgnoreCase))
        {
            groups.Add(NhsnAppConstants.Roles.NhsnLinkSysAdmin);
        }

        return groups.ToArray();
    }

    private sealed record IncomingUser(string ExternalUserId, string Email, string Name, string[] Groups, string? FacilityId);
}