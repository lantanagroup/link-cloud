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

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
