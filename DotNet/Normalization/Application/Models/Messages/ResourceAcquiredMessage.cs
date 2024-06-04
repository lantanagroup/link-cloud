using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Normalization.Application.Models.Messages;

public class ResourceAcquiredMessage
{
    public string PatientId { get; set; }
    public string QueryType { get; set; }
    public object Resource { get; set; }
    public List<ScheduledReport> ScheduledReports { get; set; }
}
