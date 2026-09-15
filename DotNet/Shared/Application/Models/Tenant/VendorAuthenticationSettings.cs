using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace LantanaGroup.Link.Shared.Application.Models.Tenant
{
    [DataContract]
    public class VendorAuthenticationSettings : IValidatableObject
    {
        private static readonly Regex KeyVaultSecretName =
            new("^[0-9a-zA-Z-]{1,127}$", RegexOptions.Compiled);

        [DataMember]
        [JsonPropertyName("signingKeySecretId")]
        public string? SigningKeySecretId { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (SigningKeySecretId is null)
            {
                yield break;
            }

            if (!KeyVaultSecretName.IsMatch(SigningKeySecretId))
            {
                yield return new ValidationResult(
                    "SigningKeySecretId must be a Key Vault secret name: 1 to 127 characters of letters, digits and dashes. Send null to clear the association.",
                    new[] { nameof(SigningKeySecretId) });
            }
        }
    }
}
