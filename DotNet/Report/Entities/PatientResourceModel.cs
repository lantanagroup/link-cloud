using LantanaGroup.Link.Report.Attributes;

namespace LantanaGroup.Link.Report.Entities
{
    [BsonCollection("patientResource")]
    public class PatientResourceModel : ReportEntity
    {
        public string FacilityId { get; set; }
        public string PatientId { get; set; }
        public string Reference { get; set; }
        public string Resource { get; set; }
    }
}
