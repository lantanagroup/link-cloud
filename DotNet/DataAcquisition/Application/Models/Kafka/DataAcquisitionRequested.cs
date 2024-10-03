namespace LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;


public class DataAcquisitionRequested
{
    public string PatientId { get; set; } = null!;
    /// <summary>
    /// Valid options: Initial, Supplemental
    /// </summary>
    public string QueryType { get; set; } = null!;
    public List<ScheduledReport> ScheduledReports { get; set; } = new List<ScheduledReport>();
    public ReportableEvent ReportableEvent { get; set; }
}
