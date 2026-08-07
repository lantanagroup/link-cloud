namespace LantanaGroup.Link.MockDmrpApi.Settings;

public class DmrpApiSettings
{
    public const string ConfigSectionName = "MockDmrpApi";

    /// <summary>
    /// Master switch. When false the entire API surface answers 503 and no schema
    /// migration runs. Production disables the service regardless of this value.
    /// </summary>
    /// <remarks>
    /// Do not read this property to decide availability. It carries only what configuration
    /// bound, so it misses the Production block that
    /// <see cref="Application.Middleware.DmrpAvailability.IsEnabled"/> applies -- reading it
    /// directly would let a production deployment serve the mock. It exists so the
    /// configuration key can be named from a symbol rather than a string literal.
    /// </remarks>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Client id the token endpoint accepts. Not a secret, so it keeps a working default.
    /// </summary>
    public string AuthClientId { get; set; } = "link-cloud-dev";

    /// <summary>
    /// Client secret the token endpoint accepts. Deliberately has no default: a deployment
    /// that fails to configure one should refuse to issue tokens rather than accept a secret
    /// published in this repository. Supplied by appsettings.Development.json on a
    /// workstation and by docker-compose locally.
    /// </summary>
    public string AuthClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// HMAC key used to sign issued tokens. HS512 requires at least 512 bits, so
    /// this must be at least 64 bytes; <see cref="Application.Services.AuthTokenService"/>
    /// fails fast if it is shorter.
    /// </summary>
    /// <remarks>
    /// Every replica must be configured with the same value. Tokens are validated by
    /// the same service that issues them, so a per-instance key means a token minted
    /// by one instance is rejected by another.
    /// <para>
    /// Deliberately has no default, so an unprovisioned deployment cannot silently sign with
    /// a value published in this repository -- anyone reading the repo could then forge a
    /// token for that environment. Supplied by appsettings.Development.json on a workstation
    /// and by docker-compose locally.
    /// </para>
    /// <para>
    /// Note where that failure surfaces. <see cref="Application.Services.AuthTokenService"/>
    /// is a lazily-constructed singleton, so a deployment missing this key still starts and
    /// still reports healthy; the first token issued or validated throws, and both the token
    /// endpoint and the contract endpoints answer 500. Provision it before enabling the mock
    /// in an environment rather than relying on the health check to notice.
    /// </para>
    /// </remarks>
    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "link-mock-dmrp";

    public string Audience { get; set; } = "dmrp-api";

    public int TokenLifetimeSeconds { get; set; } = 3600;
}
