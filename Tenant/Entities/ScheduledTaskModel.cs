
namespace LantanaGroup.Link.Tenant.Entities
{
    public class ScheduledTaskModel
    {
        public string? KafkaTopic { get; set; } = string.Empty;
        public List<ReportTypeSchedule> ReportTypeSchedules  { get; set; } = new List<ReportTypeSchedule>();

        public class ReportTypeSchedule
        {
            public string? ReportType { get; set; } 
            public  List<string>?  ScheduledTriggers { get; set; } = new List<string>();  
        }
    }

}
