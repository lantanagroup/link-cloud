using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Validation.Models
{
    public class PatientDataEvaluatedMessage
    {
        public string PatientId { get; set; }
        public string TenantId { get; set; }
        public Object Result { get; set; }
        public string MeasureId { get; set; }
    }
}
