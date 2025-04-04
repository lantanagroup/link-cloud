namespace LantanaGroup.Link.Tenant.Models
{
    public class RegenerateReportRequest
    {
        public string? ReportId { get; set; }
        public bool? BypassSubmission { get; set; }
    }
}