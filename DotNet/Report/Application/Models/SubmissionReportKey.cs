namespace LantanaGroup.Link.Report.Application.Models
{
    public class SubmissionReportKey
    {
        public string FacilityId { get; set; } = string.Empty;
        public DateTime? StartDate { get; internal set; }
        public DateTime? EndDate { get; internal set; }
    }
}
