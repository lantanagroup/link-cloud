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
    /// The name an OIDC issuer gives the email claim, which is also the name it keeps on the
    /// principal: the BFF turns inbound claim mapping off, so claims are not rewritten to the
    /// WS-Federation URIs that <see cref="ClaimTypes"/> holds.
    /// </summary>
    private const string StandardEmailClaim = "email";

    /// <summary>
    /// The configured email claim, falling back to the standard one. Null only when the principal
    /// carries none of them.
    /// </summary>
    /// <remarks>
    /// Both spellings of the standard claim are tried because the fallback is only worth having if
    /// it can fire. With MapInboundClaims off a token's <c>email</c> claim arrives named
    /// <c>email</c>, so <see cref="ClaimTypes.Email"/> alone would match nothing an issuer sends -
    /// it is kept after it for a principal assembled elsewhere, with mapping on.
    /// <para>
    /// Selection only: the value comes back as it appears on the token, blank included, because
    /// <see cref="NhsnUserContext"/> falls back to an identifier and so has to be able to see that a
    /// blank is what the token carried. A caller that wants the first claim actually holding an
    /// address wants <see cref="NonBlankEmail"/>.
    /// </para>
    /// </remarks>
    public static string? Email(ClaimsPrincipal principal, NhsnJwtSettings settings) =>
        principal.FindFirstValue(settings.EmailClaimType)
        ?? principal.FindFirstValue(StandardEmailClaim)
        ?? principal.FindFirstValue(ClaimTypes.Email);

    /// <summary>
    /// The first nonblank email claim, in the same order as <see cref="Email"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="Email"/> stops at the first claim that merely exists, so a configured claim that
    /// is present but empty hides a usable address on either fallback - and nothing rejects a blank
    /// claim value, so a gateway can send exactly that. Attributing a call needs the address rather
    /// than the position, hence a second selection rather than a change to the first: that one's
    /// blank is load-bearing for its own caller.
    /// </remarks>
    public static string? NonBlankEmail(ClaimsPrincipal principal, NhsnJwtSettings settings) =>
        new[] { settings.EmailClaimType, StandardEmailClaim, ClaimTypes.Email }
            .Select(type => principal.FindFirstValue(type))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
