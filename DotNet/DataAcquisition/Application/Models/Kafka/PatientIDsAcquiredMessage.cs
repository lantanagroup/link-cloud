using Hl7.Fhir.Model;

namespace LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;

public class PatientIDsAcquiredMessage : IBaseMessage
{
    public object PatientIds { get; set; }
}
