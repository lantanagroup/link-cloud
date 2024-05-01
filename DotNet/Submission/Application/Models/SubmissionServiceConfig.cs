namespace LantanaGroup.Link.Submission.Application.Models
{
    public class SubmissionServiceConfig
    {
        public string ReportServiceUrl { get; set; } = null!;
        public string CensusAdmittedPatientsUrl { get; set; } = null!;
        public string DataAcquisitionQueryPlanUrl { get; internal set; }
    }
}
