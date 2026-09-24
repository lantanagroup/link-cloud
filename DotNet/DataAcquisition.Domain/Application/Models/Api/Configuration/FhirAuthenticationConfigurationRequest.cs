using System.ComponentModel.DataAnnotations;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;

/// <summary>
/// Generic OAuth client-credentials settings for a facility whose EHR vendor is "Other".
/// </summary>
public class FhirAuthenticationConfigurationRequest : IValidatableObject
{
    /// <summary>
    /// The vendor's OAuth token endpoint. Must be an absolute URL.
    /// </summary>
    public string TokenUrl { get; set; } = "";

    /// <summary>
    /// The OAuth client identifier.
    /// </summary>
    public string ClientId { get; set; } = "";

    /// <summary>
    /// Plaintext secret. Written to the secret manager, never persisted on the row or returned.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// The scope requested when acquiring a token.
    /// </summary>
    public string Scope { get; set; } = "";

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var result in ValidateTokenUrl())
        {
            yield return result;
        }

        if (string.IsNullOrWhiteSpace(ClientId))
        {
            yield return new ValidationResult("ClientId is required.", new[] { nameof(ClientId) });
        }

        if (string.IsNullOrWhiteSpace(Scope))
        {
            yield return new ValidationResult("Scope is required.", new[] { nameof(Scope) });
        }

        // An absent secret means "keep the stored one", so it is only required when nothing is stored yet,
        // which the service decides. A supplied but blank one is always a mistake.
        if (ClientSecret is not null && string.IsNullOrWhiteSpace(ClientSecret))
        {
            yield return new ValidationResult("ClientSecret must not be blank when it is supplied.",
                                              new[] { nameof(ClientSecret) });
        }
    }

    private IEnumerable<ValidationResult> ValidateTokenUrl()
    {
        if (string.IsNullOrWhiteSpace(TokenUrl))
        {
            yield return new ValidationResult("TokenUrl is required.", new[] { nameof(TokenUrl) });
            yield break;
        }

        if (!Uri.TryCreate(TokenUrl.Trim(), UriKind.Absolute, out var tokenUri))
        {
            yield return new ValidationResult("TokenUrl must be a valid absolute URL.", new[] { nameof(TokenUrl) });
            yield break;
        }

        if (tokenUri.Scheme != Uri.UriSchemeHttp && tokenUri.Scheme != Uri.UriSchemeHttps)
        {
            yield return new ValidationResult("TokenUrl must use the http or https scheme.",
                                              new[] { nameof(TokenUrl) });
        }

        // RFC 6749 section 3.2: a token endpoint may carry a query string, but never a fragment.
        if (!string.IsNullOrEmpty(tokenUri.Fragment))
        {
            yield return new ValidationResult("TokenUrl must not include a fragment.", new[] { nameof(TokenUrl) });
        }
    }
}
