namespace LantanaGroup.Link.Report.Application.Models
{
    public class ReportScheduledValue
    {
        public string[] ReportTypes { get; set; }
        public string Frequency { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}
