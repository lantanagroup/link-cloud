using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.Report.Entities
{

    [BsonCollection("measureReportConfig")]
    [BsonIgnoreExtraElements]
    public class MeasureReportConfigModel : BaseEntityExtended
    {
        public string FacilityId { get; set; } = string.Empty;
        public string ReportType { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.String)]
        public BundlingType BundlingType { get; set; } = BundlingType.Default;
    }
}
