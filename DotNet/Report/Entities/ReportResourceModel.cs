using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;
using Microsoft.EntityFrameworkCore.Query.Internal;
using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.Report.Entities
{
    [BsonCollection("reportResources")]
    [BsonIgnoreExtraElements]
    public class ReportResourceModel : BaseEntityExtended
    {
        public string FacilityId { get; set; }
        public string ReportScheduledId { get; set; }
        public string PatientId { get; set; }
        public string ResourceType { get; set; }
        public string ResourceId { get; set; }
        //TODO: Add info on ResourceDetails and some of the ideas Sean mentioned on storing specific pieces of resource info to help QA and Data teams track reports
    }
}
