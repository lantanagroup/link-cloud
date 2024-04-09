using LantanaGroup.Link.Report.Attributes;
using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.Report.Entities
{

    [BsonCollection("report")]
    [BsonIgnoreExtraElements]
    public class ReportModel : ReportEntity
    {
        public string? FacilityId { get; set; }

        public string? ReportType { get; set; }
        public string? Content { get; set; }
    }
}
