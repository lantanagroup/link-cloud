using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

public class TailingMessageModel
{
    public string FacilityId { get; set; } = string.Empty;
    public ResourceAcquired ResourceAcquired { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public List<long>? LogIds { get; set; } = new List<long>();
}
