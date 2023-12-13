using LantanaGroup.Link.Report.Attributes;
using LantanaGroup.Link.Report.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.Report.Entities
{

    [BsonCollection("measureReportSubmission")]
    [BsonIgnoreExtraElements]
    public class MeasureReportSubmissionModel : ReportEntity
    {
        public string MeasureReportScheduleId { get; set; } = string.Empty;
        public string SubmissionBundle { get; set; } = string.Empty;
    }
}
