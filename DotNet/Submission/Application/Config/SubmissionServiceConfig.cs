namespace LantanaGroup.Link.Submission.Application.Config
{
    public class SubmissionServiceConfig
    {
        public string SubmissionDirectory { get; set; } = null!;
        public int PatientBundleBatchSize { get; set; } = 1;

        public List<MeasureName> MeasureNames { get; set; } = new();
    }

    public class MeasureName
    {
        public string Url { get; set; } = string.Empty;
        public string MeasureId { get; set; } = string.Empty;
        public string ShortName { get; set; } = string.Empty;

    }
}
