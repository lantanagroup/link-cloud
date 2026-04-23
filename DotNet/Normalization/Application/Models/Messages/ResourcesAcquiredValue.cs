using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.EntityFrameworkCore.Query.Internal;

namespace LantanaGroup.Link.Normalization.Application.Models.Messages;

public class ResourcesAcquiredValue
{
    public string QueryType { get; set; }
    public List<ScheduledReport> ScheduledReports { get; set; }
    public string ReportableEvent { get; set; }
    public string CacheType { get; set; }
    public List<string> CacheKeys { get; set; }
}
