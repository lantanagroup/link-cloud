namespace LantanaGroup.Link.Tenant.Entities
{

    public class FacilityConfigModel : BaseEntity
    {
        public string FacilityId { get; set; } = null!;
        public string? FacilityName { get; set; }
        public List<ScheduledTaskModel>? ScheduledTasks { get; set; } = new List<ScheduledTaskModel>();
        public List<MonthlyReportingPlanModel>? MonthlyReportingPlans { get; set; } = new List<MonthlyReportingPlanModel>();
        public DateTime? MRPModifyDate { get; set; }
        public DateTime? MRPCreatedDate { get; set; }

    }
}
