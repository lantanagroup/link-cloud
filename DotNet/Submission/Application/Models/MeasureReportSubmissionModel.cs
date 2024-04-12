using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace LantanaGroup.Link.Submission.Application.Models
{
    public class MeasureReportSubmissionModel
    {
        public string MeasureReportScheduleId { get; set; } = string.Empty;
        public string SubmissionBundle { get; set; } = string.Empty;
    }
}
