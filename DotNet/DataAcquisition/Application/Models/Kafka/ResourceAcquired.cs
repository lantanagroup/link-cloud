using Hl7.Fhir.Model;

namespace LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;

public class ResourceAcquired : IBaseMessage
{
    public string PatientId { get; set; }
    public string QueryType { get; set; }
    public Resource Resource { get; set; }
    public List<ScheduledReport> ScheduledReports { get; set; } = new List<ScheduledReport>();
}
