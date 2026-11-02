using System.Security.Claims;
using LantanaGroup.Link.Nhsn.App.Bff.Settings;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Session;

/// <summary>
/// Where the caller's details sit on a validated gateway JWT.
/// </summary>
/// <remarks>
/// One place, because two types need the email for different purposes - <see cref="NhsnUserContext"/>
/// to describe the signed-in user, and <see cref="NhsnActorEmailProvider"/> to attribute an outbound
/// call - and if the gateway ever renames the claim, the second copy is the one that gets missed.
/// </remarks>
internal static class NhsnClaims
{
    /// <summary>
    /// The configured email claim, falling back to the standard one. Null only when the principal
    /// carries neither.
    /// </summary>
    /// <remarks>
    /// Selection only: the value comes back as it appears on the token, blank included. The two
    /// callers disagree about what a blank means - one falls back to an identifier, the other treats
    /// it as naming nobody - so normalising it here would quietly change one of them.
    /// </remarks>
    public static string? Email(ClaimsPrincipal principal, NhsnJwtSettings settings) =>
        principal.FindFirstValue(settings.EmailClaimType)
        ?? principal.FindFirstValue(ClaimTypes.Email);
}
