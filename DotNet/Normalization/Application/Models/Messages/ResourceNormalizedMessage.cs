using Hl7.Fhir.Model;
using System.Text.Json;

namespace LantanaGroup.Link.Normalization.Application.Models.Messages;

public class ResourceNormalizedMessage
{
    public bool AcquisitionComplete { get; set; } = false;
    public string PatientId { get; set; }
    public string QueryType { get; set; }
    public object Resource { get; set; }
    public List<ScheduledReport> ScheduledReports { get; set; }
    public string ReportableEvent { get; set; }
}
