using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Census.Application.Models.Messages;

public class PatientEvent : IBaseMessage
{
    public string PatientId { get; set; }
    public string EventType { get; set; }
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReportTrackingId { get; set; }
}
