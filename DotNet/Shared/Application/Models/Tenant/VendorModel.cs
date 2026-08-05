using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Shared.Application.Models.Tenant
{
    [DataContract]
    public class VendorModel
    {
        [DataMember]
        [JsonPropertyName("id")]
        public Guid? Id { get; set; }

        [DataMember]
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [DataMember]
        [JsonPropertyName("authentication")]
        public VendorAuthenticationSettings? Authentication { get; set; }
    }
}
