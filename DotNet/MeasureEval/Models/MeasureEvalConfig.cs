namespace LantanaGroup.Link.MeasureEval.Models
{
    public class MeasureEvalConfig
    {
        public string TerminologyServiceUrl { get; set; } = null!;
        public string EvaluationServiceUrl { get; set; } = null!;
        public int MaxRetry { get; set; } = 10;
        public int RetryWait { get; set; } = 1000;
        public List<ReportDefinition> ReportDefinitions { get; set; } = new List<ReportDefinition>();
        public List<PackageDefinition> PackageDefinitions { get; set; } = new List<PackageDefinition>();
    }
}
