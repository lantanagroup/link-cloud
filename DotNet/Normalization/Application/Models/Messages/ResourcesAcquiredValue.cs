using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.EntityFrameworkCore.Query.Internal;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Normalization.Application.Models.Messages;

public class ResourcesAcquiredValue
{
    public string QueryType { get; set; }
    public List<ScheduledReport> ScheduledReports { get; set; }
    public string ReportableEvent { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ResourceCacheType CacheType { get; set; }
    public List<string> CacheKeys { get; set; }
}
