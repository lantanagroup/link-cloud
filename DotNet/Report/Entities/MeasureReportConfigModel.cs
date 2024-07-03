using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Shared.Domain.Attributes;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.Report.Entities
{

    [BsonCollection("measureReportConfig")]
    [BsonIgnoreExtraElements]
    public class MeasureReportConfigModel : ReportEntity
    {
        public string FacilityId { get; set; } = string.Empty;
        public string ReportType { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.String)]
        public BundlingType BundlingType { get; set; } = BundlingType.Default;
    }
}
