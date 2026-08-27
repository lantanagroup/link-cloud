using System.Security.Claims;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Enums;
using LantanaGroup.Link.Nhsn.App.Bff.Settings;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Session;

// Resolves INhsnUserContext from the validated JWT on the current request. Every member is
// computed from ClaimsPrincipal and nothing else. This type must never take a dependency on
// NhsnAppDbContext — the point of it is that authorization state has no persisted copy to drift
// from.
public sealed class NhsnUserContext : INhsnUserContext
{
    private const string FacilityAdminGroup = "FACADMIN";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly NhsnJwtSettings _jwtSettings;

    private string[]? _groups;

    public NhsnUserContext(IHttpContextAccessor httpContextAccessor, IOptions<NhsnJwtSettings> jwtOptions)
    {
        _httpContextAccessor = httpContextAccessor;
        _jwtSettings = jwtOptions.Value;
    }

    private ClaimsPrincipal Principal =>
        _httpContextAccessor.HttpContext?.User
        ?? throw new InvalidOperationException("No HttpContext is available; INhsnUserContext can only be resolved inside a request.");

    // The gateway sends this as userID while the configured claim type is userId; .NET claim
    // lookup is OrdinalIgnoreCase so that still resolves.
    public string ExternalUserId =>
        Principal.FindFirstValue(_jwtSettings.UserIdClaimType)
        ?? Principal.FindFirstValue(_jwtSettings.EmailClaimType)
        ?? Principal.Identity?.Name
        ?? throw new InvalidOperationException("Unable to resolve user identifier from the authenticated principal.");

    public string Email =>
        Principal.FindFirstValue(_jwtSettings.EmailClaimType)
        ?? Principal.FindFirstValue(ClaimTypes.Email)
        ?? ExternalUserId;

    public string Name =>
        Principal.FindFirstValue(_jwtSettings.NameDisplayClaimType)
        ?? Principal.FindFirstValue(_jwtSettings.NameClaimType)
        ?? Principal.Identity?.Name
        ?? Email;

    public IReadOnlyList<string> Groups => _groups ??= ResolveGroups();

    // The gateway sends facility as a JSON number (e.g. 20759); the JWT handler surfaces it as its
    // string form, which is what the column stores.
    public string? FacilityId
    {
        get
        {
            var facilityId = Principal.FindFirstValue(_jwtSettings.FacilityIdClaimType);
            return string.IsNullOrWhiteSpace(facilityId) ? null : facilityId.Trim();
        }
    }

    public bool HasFacility => FacilityId is not null;

    public bool IsFacilityAdmin => Groups.Contains(FacilityAdminGroup, StringComparer.OrdinalIgnoreCase);

    public NhsnAccessState AccessState =>
        !HasFacility ? NhsnAccessState.MissingFacility
        : !IsFacilityAdmin ? NhsnAccessState.MissingRequiredRole
        : NhsnAccessState.Allowed;

    public string RequireFacilityId() =>
        FacilityId ?? throw new InvalidOperationException("The authenticated token carries no facility claim.");

    // groups is a JSON array; the JWT handler emits one claim per value.
    private string[] ResolveGroups() =>
        Principal.FindAll(_jwtSettings.GroupsClaimType)
            .Select(claim => claim.Value)
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Select(group => group.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
