using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.Tenant.Entities
{
    [BsonCollection("Facilities")]
    [BsonIgnoreExtraElements]
    public class FacilityConfigModel : BaseEntity
    {
        public string  FacilityId { get; set; } = null!;
        public string? FacilityName { get; set; }
        public List<ScheduledTaskModel>? ScheduledTasks { get; set; } = new List<ScheduledTaskModel>();
        public List<MonthlyReportingPlanModel>? MonthlyReportingPlans { get; set; } = new List<MonthlyReportingPlanModel>();
        public DateTime MRPModifyDate { get; set; }
        public DateTime MRPCreatedDate { get; set; }

    }

}
