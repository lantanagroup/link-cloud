namespace LantanaGroup.Link.Report.Models
{
    public class ReportEntrySummary
    {
        public Dictionary<string, int> ReportTypeCounts { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> ReportingStatusCounts { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> SubmissionStatusCounts { get; set; } = new Dictionary<string, int>();

    /// <summary>Stored per-type resource counts summed over every entry's measure reports (same convention as the per-patient counts).</summary>
    public Dictionary<string, int> ResourceTypeCounts { get; set; } = new Dictionary<string, int>();
    }
}
