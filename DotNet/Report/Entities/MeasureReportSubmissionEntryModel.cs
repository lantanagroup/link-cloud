using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Attributes;
using LantanaGroup.Link.Report.Serializers;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.Report.Entities
{

    [BsonCollection("measureReportSubmissionEntry")]
    [BsonIgnoreExtraElements]
    public class MeasureReportSubmissionEntryModel : ReportEntity
    {

        public string FacilityId { get; set; } = string.Empty;
        public string MeasureReportScheduleId { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        public string MeasureReport { get; set; } = string.Empty;
        public List<string> ContainedResources { get; set; } = new List<string>();

    }
}
