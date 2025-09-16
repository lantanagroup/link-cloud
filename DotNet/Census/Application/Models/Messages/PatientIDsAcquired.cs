using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Census.Application.Models.Messages;

public class PatientIDsAcquired : IBaseMessage
{
    public List PatientIds { get; set; }

    public string ReportTrackingId { get; set; }  = String.Empty;
}
