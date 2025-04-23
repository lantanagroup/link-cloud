namespace LantanaGroup.Link.Shared.Application.Models.Tenant
{
    public class RegenerateReportRequest
    {
        public string? ReportId { get; set; }
        public bool? BypassSubmission { get; set; }
    }
}