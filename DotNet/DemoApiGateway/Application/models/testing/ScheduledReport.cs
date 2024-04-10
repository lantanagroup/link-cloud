namespace LantanaGroup.Link.DemoApiGateway.Application.models.testing
{
    public class ScheduledReport
    {
        public string ReportType { get; set; } = string.Empty;
        public DateTime StartDate { get; set; } = DateTime.Now;
        public DateTime EndDate { get; set; } = DateTime.Now;
    }
}
