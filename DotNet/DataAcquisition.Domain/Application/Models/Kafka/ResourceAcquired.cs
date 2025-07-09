using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;

public class ResourceAcquired
{
    public bool AcquisitionComplete { get; set; } = false;
    public string PatientId { get; set; } = string.Empty;
    public string QueryType { get; set; } = string.Empty;
    public Resource Resource { get; set; } = null!;
    public List<ScheduledReport> ScheduledReports { get; set; } = new List<ScheduledReport>();
    public ReportableEvent ReportableEvent { get; set; } = ReportableEvent.Discharge;
}
