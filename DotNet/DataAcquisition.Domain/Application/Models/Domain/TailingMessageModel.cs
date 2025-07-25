using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;

public class TailingMessageModel
{
    public string Key { get; set; } = string.Empty;
    public ResourceAcquired ResourceAcquired { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public List<string>? LogIds { get; set; } = new List<string>();
}
