using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Shared.Application.Models.Kafka;

public class ResourceKey
{
    [JsonPropertyName("facilityId")]
    public string FacilityId { get; set; } = string.Empty;
    [JsonPropertyName("patientId")]
    public string PatientId { get; set; } = string.Empty;
}
