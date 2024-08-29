using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;
using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.Report.Entities
{

    [BsonCollection("measureReportSubmission")]
    [BsonIgnoreExtraElements]
    public class MeasureReportSubmissionModel : BaseEntityExtended
    {
        public string MeasureReportScheduleId { get; set; } = string.Empty;
        public Bundle SubmissionBundle { get; set; }
    }
}
