namespace LantanaGroup.Link.Tenant.Models;

public class GenerateAdhocReportResponse(Guid reportId)
{
    public Guid ReportId { get; set; } = reportId;
}