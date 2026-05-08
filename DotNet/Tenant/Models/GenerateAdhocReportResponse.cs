namespace LantanaGroup.Link.Tenant.Models;

public class GenerateAdhocReportResponse(string reportId)
{
    public string ReportId { get; set; } = reportId;
}