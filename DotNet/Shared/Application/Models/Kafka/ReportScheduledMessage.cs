namespace LantanaGroup.Link.Shared.Application.Models.Kafka;

public class ReportScheduledMessage
{
    public string? FacilityId { get; set; }
    public string[] ReportTypes { get; set; }
    public string Frequency { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? ReportTrackingId { get; set; }
}
