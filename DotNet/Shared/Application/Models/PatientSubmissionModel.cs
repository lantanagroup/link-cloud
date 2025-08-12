using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Shared.Application.Models
{
    public class PatientSubmissionModel
    {
        public string FacilityId { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        public string ReportScheduleId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public Bundle Bundle { get; set; }
    }
}
