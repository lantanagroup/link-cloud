namespace LantanaGroup.Automation.Configuration;

/// <summary>
/// OAuth configuration for token-based authentication.
/// </summary>
public class OAuthConfig
{
    public bool ShouldAuthenticate { get; set; }
    public string? TokenEndpoint { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string Scope { get; set; } = "openid profile email";
    public string? Username { get; set; }
    public string? Password { get; set; }
}
