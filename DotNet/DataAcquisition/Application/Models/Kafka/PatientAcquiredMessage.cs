using Hl7.Fhir.Model;

namespace LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;

public class PatientAcquiredMessage
{
    public string PatientId { get; set; }
    public object PatientBundle { get; set; }
    public List<ScheduledReport> ScheduledReports { get; set; }
}
