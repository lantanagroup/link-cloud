using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Shared.Application.Models.Tenant
{
    [DataContract]
    public class VendorVersionModel
    {
        [DataMember]
        [JsonPropertyName("id")]
        public Guid? Id { get; set; }

        [DataMember]
        [JsonPropertyName("vendorId")]
        public Guid? VendorId { get; set; }

        [DataMember]
        [JsonPropertyName("vendorName")]
        public string? VendorName { get; set; }

        [DataMember]
        [JsonPropertyName("version")]
        [Required]
        public string? Version { get; set; }

        [DataMember]
        [JsonPropertyName("authentication")]
        public VendorAuthenticationSettings? Authentication { get; set; }
    }
}
