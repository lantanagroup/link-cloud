using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Shared.Application.Models.Kafka;

public class ResourceKey
{
    [JsonPropertyName("facilityId")]
    public string FacilityId { get; set; } = string.Empty;
    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; set; } = string.Empty;
}
