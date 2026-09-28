namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;

/// <summary>
/// The stored configuration for a facility whose EHR vendor is "Other".
/// </summary>
public class FhirAuthenticationConfigurationResponse
{
    /// <summary>
    /// Url of the vendor's OAuth token endpoint. Must be an absolute URL.
    /// </summary>
    public string TokenUrl { get; set; } = "";

    /// <summary>
    /// The OAuth client identifier.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// The scope requested when acquiring a token.
    /// </summary>
    public string Scope { get; set; } = "";

    /// <summary>
    /// Indicates whether the client secret is stored in the secret manager. If true, the client
    /// secret is not returned in this response.
    /// </summary>
    public bool ClientSecretStored { get; set; }
}
