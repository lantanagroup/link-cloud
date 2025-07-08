namespace LantanaGroup.Link.Shared.Application.Models.Responses
{
    public class ReportScheduleSummaryModel
    {
        public string ReportId { get; set; } = string.Empty;
        public string FacilityId { get; set; } = string.Empty;
        public DateTime StartDate { get; set; } = default;
        public DateTime EndDate { get; set; } = default;
        public DateTime? SubmitReportDateTime { get; set; }
        public List<string> Measures { get; set; } = [];
    }
}
