using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Normalization.Application.Models.Messages;

public class ResourcesNormalizedMessage
{
    public string QueryType { get; set; }
    public List<ScheduledReport> ScheduledReports { get; set; }
    public string ReportableEvent { get; set; }
    public ResourceCacheType CacheType { get; set; }
    public List<string> CacheKeys { get; set; }
}
