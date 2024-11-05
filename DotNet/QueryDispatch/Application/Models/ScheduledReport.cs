namespace LantanaGroup.Link.QueryDispatch.Application.Models
{
    public class ScheduledReport
    {
        public List<string> ReportTypes { get; set; }
        public string Frequency { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}
