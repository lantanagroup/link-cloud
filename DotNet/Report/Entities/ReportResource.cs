using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;
using Microsoft.EntityFrameworkCore.Query.Internal;
using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.Report.Entities
{
    [BsonCollection("reportResources")]
    [BsonIgnoreExtraElements]
    public class ReportResource : BaseEntityExtended
    {
        public string FacilityId { get; set; }
        public string ReportScheduledId { get; set; }
        public string PatientId { get; set; }
        public string MeasureReportId { get; set; }
        public string ResourceType { get; set; }
        public string ResourceId { get; set; }
        //Note: Daniel - @Sean mentioned that there may be a need to store key pieces of resource information (codes, dates, etc) for UI purposes. We can add an additional 'ResourceDetails' property here to keep tack of those things if we need to. 
    }
}