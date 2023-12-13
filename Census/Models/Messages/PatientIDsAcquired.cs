using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Census.Models.Messages;

public class PatientIDsAcquired : IBaseMessage
{
    public List PatientIds { get; set; }
}
