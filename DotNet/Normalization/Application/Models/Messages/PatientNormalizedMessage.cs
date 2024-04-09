using Hl7.Fhir.Model;
using System.Text.Json;

namespace LantanaGroup.Link.Normalization.Application.Models.Messages;

public class PatientNormalizedMessage
{
    public string PatientId { get; set; }
    public object PatientBundle { get; set; }
    public List<ScheduledReport> ScheduledReports { get; set; }
}
