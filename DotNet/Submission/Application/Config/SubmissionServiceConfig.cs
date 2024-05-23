namespace LantanaGroup.Link.Submission.Application.Config
{
    public class SubmissionServiceConfig
    {
        public string ReportServiceUrl { get; set; } = null!;
        public string CensusUrl { get; set; } = null!;
        public string DataAcquisitionUrl { get; set; } = null!;
        public string SubmissionDirectory { get; set; } = null!;
        public int PatientBundleBatchSize { get; set; } = 1;

        public List<MeasureName> MeasureNames { get; set; }
    }

    public class MeasureName
    {
        public string Url { get; set; }
        public string MeasureId { get; set; }
        public string ShortName { get; set; }

    }
}
