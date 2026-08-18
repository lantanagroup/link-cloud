namespace LantanaGroup.Automation.Configuration;

/// <summary>
/// Basic authentication configuration (username/password).
/// </summary>
public class BasicAuthConfig
{
    public bool ShouldAuthenticate { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
}
