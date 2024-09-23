using CronExpressionDescriptor;

namespace LantanaGroup.Link.Tenant.Models
{
    public class ScheduledReportDto
    {
        public string[] Daily { get; set; }

        public string[] Weekly { get; set; }

        public string[] Monthly { get; set; }
    }

}
