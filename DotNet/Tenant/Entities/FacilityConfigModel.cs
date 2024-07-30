using LantanaGroup.Link.Shared.Domain.Entities;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.Tenant.Entities
{
    [Table("Facilities")]
    public class FacilityConfigModel : BaseEntityExtended
    {
        public string FacilityId { get; set; } = null!;
        public string? FacilityName { get; set; }
        public List<ScheduledTaskModel>? ScheduledTasks { get; set; } = new List<ScheduledTaskModel>();
        public List<MonthlyReportingPlanModel>? MonthlyReportingPlans { get; set; } = new List<MonthlyReportingPlanModel>();
        public DateTime? MRPModifyDate { get; set; }
        public DateTime? MRPCreatedDate { get; set; }

    }
}
