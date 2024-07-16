using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;
using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.Report.Entities
{

    [BsonCollection("report")]
    [BsonIgnoreExtraElements]
    public class ReportModel : BaseEntityExtended
    {
        public string? FacilityId { get; set; }

        public string? ReportType { get; set; }
        public string? Content { get; set; }
    }
}
