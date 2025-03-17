using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Report.Application.Models
{
    public class ValidationCompleteValue
    {
        public string PatientId { get; set; }
        public bool IsValid { get; set; }
        public string ReportTrackingId { get; set; }
    }
}
