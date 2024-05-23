using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Submission.Application.Models
{
    public class MeasureReportSubmissionModel
    {
        public string FacilityId { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public Bundle? PatientResources { get; set; }
        public Bundle? OtherResources { get; set; }
    }
}
