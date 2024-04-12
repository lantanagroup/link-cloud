namespace LantanaGroup.Link.DemoApiGateway.Application.models.interfaces.testing
{
    public interface IReportScheduled
    {
        string FacilityId { get; set; }
        string ReportType { get; set; } 
        DateTime? StartDate { get; set; } 
        DateTime? EndDate { get; set; }
    }
}
