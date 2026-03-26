using System.Text.Json.Serialization;
using LantanaGroup.Link.LinkAdmin.BFF.Application.SerDes;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Health;

public record LinkServiceHealthReportSummary
{
    public string Service { get; set; } = string.Empty;
    [JsonConverter(typeof(HealthStatusJsonConverter))]
    public HealthStatus Status { get; set; }
    [JsonConverter(typeof(LinkServiceHealthStatusJsonConverter))]
    public LinkServiceHealthStatus KafkaConnection { get; set; }
    [JsonConverter(typeof(LinkServiceHealthStatusJsonConverter))]
    public LinkServiceHealthStatus DatabaseConnection { get; set; }
    [JsonConverter(typeof(LinkServiceHealthStatusJsonConverter))]
    public LinkServiceHealthStatus CacheConnection { get; set; }
}

public enum LinkServiceHealthStatus
{
    Healthy,
    Unhealthy,
    NotApplicable,
    Unknown
}

public static class LinkServiceHealthReportExtensions
{
    public static LinkServiceHealthReportSummary FromDomain(LinkServiceHealthReport report)
    {
        return new LinkServiceHealthReportSummary
        {
            Service = report.Service,
            Status = report.Status,
            KafkaConnection = 
                report.Entries.ContainsKey(HealthCheckType.Kafka.ToString()) ?
                    report.Entries.Any(x => x.Key.Equals(HealthCheckType.Kafka.ToString(), StringComparison.OrdinalIgnoreCase) && x.Value.Status == HealthStatus.Healthy) ? LinkServiceHealthStatus.Healthy: LinkServiceHealthStatus.Unhealthy :
                    LinkServiceHealthStatus.NotApplicable,
            DatabaseConnection = 
                report.Entries.ContainsKey(HealthCheckType.Database.ToString()) ?
                    report.Entries.Any(x => x.Key.Equals(HealthCheckType.Database.ToString(), StringComparison.OrdinalIgnoreCase) && x.Value.Status == HealthStatus.Healthy) ? LinkServiceHealthStatus.Healthy: LinkServiceHealthStatus.Unhealthy :
                    LinkServiceHealthStatus.NotApplicable,
            CacheConnection = 
                report.Entries.ContainsKey(HealthCheckType.Cache.ToString()) ?
                    report.Entries.Any(x => x.Key.Equals(HealthCheckType.Cache.ToString(), StringComparison.OrdinalIgnoreCase) && x.Value.Status == HealthStatus.Healthy) ? LinkServiceHealthStatus.Healthy: LinkServiceHealthStatus.Unhealthy :
                    LinkServiceHealthStatus.NotApplicable
        };
    }
}

