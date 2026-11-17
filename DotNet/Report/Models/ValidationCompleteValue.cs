using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Report.Models
{
    public class ValidationCompleteValue
    {
        public string? FacilityId { get; set; }
        public string PatientId { get; set; }
        public bool IsValid { get; set; }
        public string ReportTrackingId { get; set; }
    }
}
