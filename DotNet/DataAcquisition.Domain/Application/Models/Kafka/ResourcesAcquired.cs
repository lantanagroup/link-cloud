using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;

public class ResourcesAcquired
{
    public string QueryType { get; set; } = string.Empty;
    public List<ScheduledReport> ScheduledReports { get; set; } = new List<ScheduledReport>();
    public ReportableEvent ReportableEvent { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ResourceCacheType CacheType { get; set; }
    public List<string> CacheKeys { get; set; } = new List<string>();
}