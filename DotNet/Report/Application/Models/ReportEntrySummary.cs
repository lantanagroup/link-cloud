namespace LantanaGroup.Link.Report.Application.Models
{
    public class ReportEntrySummary
    {
        public Dictionary<string, int> ReportTypeCounts { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> ReportingStatusCounts { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> SubmissionStatusCounts { get; set; } = new Dictionary<string, int>();
    }
}
