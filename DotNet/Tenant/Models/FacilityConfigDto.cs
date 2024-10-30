using System.Runtime.Serialization;
using LantanaGroup.Link.Tenant.Entities;

namespace LantanaGroup.Link.Tenant.Models
{
    [DataContract]
    public class FacilityConfigDto
    {
        [DataMember]
        public string? Id { get; set; }
        public string? FacilityId { get; set; }
        [DataMember]
        public string? FacilityName { get; set; }
        public List<ScheduledTaskDto>? ScheduledTasks { get; set; } = new List<ScheduledTaskDto>();
        public List<MonthlyReportingPlanModel>? MonthlyReportingPlans { get; set; } = new List<MonthlyReportingPlanModel>();
        public DateTime? CreateDate { get; set; }
        public DateTime? ModifyDate { get; set; }
    }
}
