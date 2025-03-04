namespace LantanaGroup.Link.Normalization.Application.Models.Messages;

public class ScheduledReport
{
    public string ReportTrackingId { get; set; } = string.Empty;    
    public string[] ReportTypes { get; set; }
    public string StartDate { get; set; }
    public string EndDate { get; set; }
    public string Frequency { get; set; } = string.Empty;
}
