namespace LantanaGroup.Link.Report.Models
{
    public class ReportEntrySummary
    {
        public Dictionary<string, int> ReportTypeCounts { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> ReportingStatusCounts { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> SubmissionStatusCounts { get; set; } = new Dictionary<string, int>();
    }
}
