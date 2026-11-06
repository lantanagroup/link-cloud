using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Link.AspireAppHost.Extensions;

namespace Link.AspireAppHost.Security;

/// <summary>
/// A Link bearer token for the Link system account, so this AppHost's own calls (Create user, Seed data)
/// are accepted by services that require authentication.
/// </summary>
/// <remarks>
/// It mirrors <c>DotNet/Shared/Application/Services/Security/Token/CreateSystemToken.cs</c>: HS512 over
/// the shared local signing key, the run's issuer (<see cref="Issuer"/>), audience <c>LinkServices</c>, and the
/// system account's subject, administrator role and <c>IsLinkAdmin</c> permission
/// (<c>Link.Authorization/LinkAuthorizationConstants.cs</c>). The email claim is what a receiving service
/// records as the actor in audit events, so it names this AppHost. Written out by hand so the AppHost
/// needs no JWT package.
/// </remarks>
internal static class LinkSystemToken
{
    /// <summary>
    /// The issuer of Link bearer tokens in this run, which is also the issuer the services accept. Set once at
    /// startup (Program.cs): <c>LinkServiceAdmin</c>, as compose uses, or Keycloak's realm in Auth:Mode Keycloak.
    /// </summary>
    public static string Issuer { get; set; } = DotNetServiceExtensions.LocalTokenAuthority;

    public const string Audience = "LinkServices";

    /// <summary>The actor audit events record for changes this AppHost makes.</summary>
    public const string ActorEmail = "aspire-apphost@localhost";

    public static string Create(TimeSpan? lifetime = null)
    {
        var now = DateTimeOffset.UtcNow;
        var header = new Dictionary<string, object> { ["alg"] = "HS512", ["typ"] = "JWT" };
        var payload = new Dictionary<string, object>
        {
            ["email"] = ActorEmail,
            ["sub"] = "SystemAccount",
            ["roles"] = "LinkAdministrator",
            ["permissions"] = "IsLinkAdmin",
            ["iss"] = Issuer,
            ["aud"] = Audience,
            ["nbf"] = now.ToUnixTimeSeconds(),
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.Add(lifetime ?? TimeSpan.FromMinutes(5)).ToUnixTimeSeconds()
        };

        var unsigned = $"{Base64Url(JsonSerializer.SerializeToUtf8Bytes(header))}.{Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload))}";
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(DotNetServiceExtensions.LocalSigningKey));
        return $"{unsigned}.{Base64Url(hmac.ComputeHash(Encoding.ASCII.GetBytes(unsigned)))}";
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
