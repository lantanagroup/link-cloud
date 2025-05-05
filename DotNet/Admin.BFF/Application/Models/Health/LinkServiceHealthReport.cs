using System.Text.Json.Serialization;
using LantanaGroup.Link.LinkAdmin.BFF.Application.SerDes;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Health;

public class LinkServiceHealthReport
{
    public string Service { get; set; } = string.Empty;
    [JsonConverter(typeof(HealthStatusJsonConverter))]
    public HealthStatus Status { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public Dictionary<string, LinkServiceHealthReportEntry> Entries { get; set; } = new Dictionary<string, LinkServiceHealthReportEntry>();
}

public class LinkServiceHealthReportEntry
{
    [JsonConverter(typeof(HealthStatusJsonConverter))]
    public HealthStatus Status { get; set; }
    public TimeSpan Duration { get; set; }
}
