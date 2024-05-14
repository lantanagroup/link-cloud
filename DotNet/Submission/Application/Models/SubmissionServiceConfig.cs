namespace LantanaGroup.Link.Submission.Application.Models
{
    public class SubmissionServiceConfig
    {
        public string ReportServiceUrl { get; set; } = null!;
        public string CensusUrl { get; set; } = null!;
        public string DataAcquisitionUrl { get; set; } = null!;
        public string SubmissionDirectory { get; set; } = null!;
        public int PatientBundleBatchSize { get; set; } = 1;
    }
}
