using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Shared.Application.Models.Kafka;

/// <summary>
/// Groups status updates for one patient within one report. ReportId is the value's ReportScheduleId.
/// Producers must use Java-compatible Murmur2 partitioning; see dev-docs/report-status-events.md.
/// </summary>
public class ReportStatusUpdatedKey
{
    [JsonPropertyName("reportId")]
    [JsonPropertyOrder(0)]
    public required string ReportId { get; set; }

    [JsonPropertyName("patientId")]
    [JsonPropertyOrder(1)]
    public required string PatientId { get; set; }
}
