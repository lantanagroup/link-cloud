namespace LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;

public class DataAcquisitionRequestedMessage : IBaseMessage
{
    public string PatientId { get; set; }
    public List<ScheduledReport> ScheduledReports { get; set; }
}
