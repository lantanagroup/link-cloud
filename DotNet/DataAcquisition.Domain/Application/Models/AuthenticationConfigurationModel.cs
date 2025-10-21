using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace DataAcquisition.Domain.Application.Models;
public class AuthenticationConfigurationModel : IValidatableObject
{
    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AuthType { get; set; }

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Key { get; set; }

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TokenUrl { get; set; }

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Audience { get; set; }

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClientId { get; set; }

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UserName { get; set; }

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Password { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(AuthType))
            yield return new ValidationResult("AuthType is required.", new[] { nameof(AuthType) });

        if (AuthType?.Equals("Basic", StringComparison.OrdinalIgnoreCase) == true)
        {
            if (string.IsNullOrWhiteSpace(UserName))
                yield return new ValidationResult("UserName is required for Basic authentication.", new[] { nameof(UserName) });
            if (string.IsNullOrWhiteSpace(Password))
                yield return new ValidationResult("Password is required for Basic authentication.", new[] { nameof(Password) });
        }

        if (AuthType?.Equals("OAuth2", StringComparison.OrdinalIgnoreCase) == true)
        {
            if (string.IsNullOrWhiteSpace(TokenUrl))
                yield return new ValidationResult("TokenUrl is required for OAuth2 authentication.", new[] { nameof(TokenUrl) });
            if (string.IsNullOrWhiteSpace(ClientId))
                yield return new ValidationResult("ClientId is required for OAuth2 authentication.", new[] { nameof(ClientId) });
            if (string.IsNullOrWhiteSpace(Audience))
                yield return new ValidationResult("Audience is required for OAuth2 authentication.", new[] { nameof(Audience) });
        }
    }

    public AuthenticationConfiguration ToDomain()
    {
        return new AuthenticationConfiguration
        {
            AuthType = this.AuthType,
            Key = this.Key,
            TokenUrl = this.TokenUrl,
            Audience = this.Audience,
            ClientId = this.ClientId,
            UserName = this.UserName,
            Password = this.Password
        };
    }

    public static AuthenticationConfigurationModel FromDomain(AuthenticationConfiguration config)
    {
        return new AuthenticationConfigurationModel
        {
            AuthType = config.AuthType,
            Key = config.Key,
            TokenUrl = config.TokenUrl,
            Audience = config.Audience,
            ClientId = config.ClientId,
            UserName = config.UserName,
            Password = config.Password
        };
    }
}

