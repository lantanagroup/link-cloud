namespace LantanaGroup.Link.MockDmrpApi.Settings;

public class DmrpApiSettings
{
    public const string ConfigSectionName = "MockDmrpApi";

    /// <summary>
    /// Master switch. When false the entire API surface answers 503 and no schema
    /// migration runs. Production disables the service regardless of this value.
    /// </summary>
    public bool Enabled { get; set; } = true;

    public string AuthClientId { get; set; } = "link-cloud-dev";

    public string AuthClientSecret { get; set; } = "link-cloud-dev-secret";

    /// <summary>
    /// HMAC key used to sign issued tokens. HS512 requires at least 512 bits, so
    /// this must be at least 64 bytes; <see cref="Application.Services.AuthTokenService"/>
    /// fails fast if it is shorter.
    /// </summary>
    /// <remarks>
    /// Every replica must be configured with the same value. Tokens are validated by
    /// the same service that issues them, so a per-instance key means a token minted
    /// by one instance is rejected by another.
    /// </remarks>
    public string SigningKey { get; set; } =
        "mock-dmrp-local-development-signing-key-not-for-any-deployed-environment";

    public string Issuer { get; set; } = "link-mock-dmrp";

    public string Audience { get; set; } = "dmrp-api";

    public int TokenLifetimeSeconds { get; set; } = 3600;
}
