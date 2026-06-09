using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;

public class TailingMessageModel
{
    public string FacilityId { get; set; } = string.Empty;
    public ResourcesAcquired ResourcesAcquired { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public List<long>? LogIds { get; set; } = new List<long>();
    public string? TraceParentId { get; set; } = string.Empty;
}
