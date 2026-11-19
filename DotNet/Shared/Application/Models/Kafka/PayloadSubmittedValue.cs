using System.Text.Json.Serialization;
using LantanaGroup.Link.Shared.Application.Enums;

namespace LantanaGroup.Link.Shared.Application.Models.Kafka;

public class PayloadSubmittedValue
{
    public required PayloadType PayloadType { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FacilityId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? ReportScheduleId { get; set; }
    public string? PatientId { get; set; }
}