using LantanaGroup.Link.DemoApiGateway.Application.models.interfaces.testing;

namespace LantanaGroup.Link.DemoApiGateway.Application.models.testing
{
    public class ReportScheduled : IReportScheduled
    {
        public string FacilityId { get; set; } = string.Empty;
        public string ReportType { get; set; } = string.Empty;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class ReportScheduledMessage
    {
        public List<KeyValuePair<string, object>> Parameters { get; set; }
    }

    public class ReportScheduledKey
    {
        public string? FacilityId { get; set; }
        public string? ReportType { get; set; }

        public static implicit operator string(ReportScheduledKey v)
        {
            throw new NotImplementedException();
        }
    }
    
}
