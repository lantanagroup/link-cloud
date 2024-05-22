namespace LantanaGroup.Link.Submission.Application.Config
{
    public class SubmissionServiceConfig
    {
        public string ReportServiceUrl { get; set; } = null!;
        public string CensusUrl { get; set; } = null!;
        public string DataAcquisitionUrl { get; set; } = null!;
        public string SubmissionDirectory { get; set; } = null!;
        public int PatientBundleBatchSize { get; set; } = 1;
        public Dictionary<string, string> MeasureUrls { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> MeasureIds { get; set; } = new Dictionary<string, string>();
    }
}
