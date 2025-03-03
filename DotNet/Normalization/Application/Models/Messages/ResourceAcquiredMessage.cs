namespace LantanaGroup.Link.Normalization.Application.Models.Messages;

public class ResourceAcquiredMessage
{
    public bool AcquisitionComplete { get; set; } = false;
    public string PatientId { get; set; }
    public string QueryType { get; set; }
    public object Resource { get; set; }
    public string ReportableEvent { get; set; }
    public List<ScheduledReport> ScheduledReports { get; set; }
}
