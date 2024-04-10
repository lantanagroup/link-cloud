using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Normalization.Application.Models.Messages;

public class PatientDataAcquiredMessage
{
    public string PatientId { get; set; }
    public object PatientBundle { get; set; }
    public List<ScheduledReport> ScheduledReports { get; set; }
}
