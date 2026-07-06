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
        var user = await _dbContext.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.ExternalUserId == incomingUser.ExternalUserId, cancellationToken);

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
                IsOnboarded = false,
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
            user.LastModifiedBy = "userinfo";
            user.LastModifiedOn = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var roleNames = user.UserRoles.Select(x => x.Role.Name).OrderBy(x => x).ToArray();
        var isSystemAdmin = roleNames.Contains(NhsnAppConstants.Roles.SystemAdmin, StringComparer.OrdinalIgnoreCase);
        var availableNavigation = isSystemAdmin
            ? new[] { "users" }
            : user.IsOnboarded
                ? new[] { "maintenance", "configuration-overview", "configuration-changes" }
                : new[] { "onboarding" };

        return new UserInfoResponse
        {
            Email = user.Email,
            Name = user.Name,
            Roles = roleNames,
            IsSystemAdmin = isSystemAdmin,
            IsOnboarded = user.IsOnboarded,
            FacilityId = user.FacilityId,
            Groups = SplitGroups(user.GroupsRaw),
            AvailableNavigation = availableNavigation
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

    private static string[] SplitGroups(string? groupsRaw)
    {
        return string.IsNullOrWhiteSpace(groupsRaw)
            ? []
            : groupsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private sealed record IncomingUser(string ExternalUserId, string Email, string Name, string[] Groups, string? FacilityId);
}