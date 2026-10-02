using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Normalization.Application.Models.Messages;

public class ResourcesNormalizedValue
{
    public string QueryType { get; set; }
    public List<ScheduledReport> ScheduledReports { get; set; }
    public string ReportableEvent { get; set; }
    public string CacheKey { get; set; }
}
