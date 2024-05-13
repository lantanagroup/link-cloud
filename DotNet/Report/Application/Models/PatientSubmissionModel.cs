using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Report.Entities
{
    public class PatientSubmissionModel : ReportEntity
    {
        public string FacilityId { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string PatientResources { get; set; }
        public string OtherResources { get; set;  }
    }
}
