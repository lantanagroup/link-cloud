namespace LantanaGroup.Link.Shared.Application.Models.Responses
{
    public class ReportScheduleSummaryModel
    {
        public string ReportId { get; set; }
        public string FacilityId { get; set; }
        public DateTime StartDate { get; set; } = default;
        public DateTime EndDate { get; set; } = default;
        public DateTime? SubmitReportDateTime { get; set; }
    }
}
