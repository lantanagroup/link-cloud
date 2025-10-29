namespace LantanaGroup.Link.Census.Application.Models.Messages;

public class PatientEventMessage : IBaseMessage
{
    public string PatientId { get; set; }
    public string EventType { get; set; }

    public string ReportTrackingId { get; set; }
}
