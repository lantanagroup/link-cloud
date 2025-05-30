using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;

public class ResourceAcquired
{
    public bool AcquisitionComplete { get; set; } = false;
    public string PatientId { get; set; }
    public string QueryType { get; set; }
    public Resource Resource { get; set; }
    public List<ScheduledReport> ScheduledReports { get; set; } = new List<ScheduledReport>();
    public ReportableEvent ReportableEvent { get; set; }
}
