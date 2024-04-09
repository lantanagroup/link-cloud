namespace LantanaGroup.Link.DemoApiGateway.Application.models
{
    public class FacilityConfigModel
    {
        public string? Id { get; set; }
        public string FacilityId { get; set; } = null!;
        public string? FacilityName { get; set; }
        public List<ScheduledTask>? ScheduledTasks { get; set; } = new List<ScheduledTask>();
        public List<MonthlyReportingPlanModel>? MonthlyReportingPlans { get; set; } = new List<MonthlyReportingPlanModel>();
        public DateTime MRPModifyDate { get; set; }
        public DateTime MRPCreatedDate { get; set; }
    }

    public class ScheduledTask
    {
        public string? KafkaTopic { get; set; } = string.Empty;

        public List<ReportTypeSchedule> ReportTypeSchedules { get; set; } = new List<ReportTypeSchedule>();
    }

    public class ReportTypeSchedule
    {
        public string ReportType { get; set; }
        public List<string> ScheduledTriggers { get; set; } = new List<string>();
    }

    public class MonthlyReportingPlanModel
    {
        public string? ReportType { get; set; }
        public int? ReportMonth { get; set; }
        public int? ReportYear { get; set; }
    }
}
