namespace LantanaGroup.Link.Report.Application.Models
{
    public class SubmitReportKey
    {
        public string FacilityId { get; set; } = string.Empty;
        public DateTime? StartDate { get; internal set; }
        public DateTime? EndDate { get; internal set; }
        public string? ReportScheduleId { get; set; }
    }
}
