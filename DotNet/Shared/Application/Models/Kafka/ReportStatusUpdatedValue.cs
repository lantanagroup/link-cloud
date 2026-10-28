using System.Text.Json.Serialization;
using LantanaGroup.Link.Shared.Application.Enums.Reporting;

namespace LantanaGroup.Link.Shared.Application.Models.Kafka;

/// <summary>
/// A patient stage's completion or failure.
/// See dev-docs/report-status-events.md for the contract and transition semantics.
/// </summary>
public class ReportStatusUpdatedValue
{
    [JsonPropertyName("reportScheduleId")]
    public required string ReportScheduleId { get; set; }

    [JsonPropertyName("patientId")]
    public required string PatientId { get; set; }

    [JsonPropertyName("status")]
    public required PatientReportingStatus Status { get; set; }

    [JsonPropertyName("isSuccess")]
    public required bool IsSuccess { get; set; }

    /// <summary>Stage duration in milliseconds, excluding consumer lag.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("processingTime")]
    public long? ProcessingTime { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; set; }

    /// <summary>Populated by Validation on the Validation stage.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("validationResult")]
    public ValidationResult? ValidationResult { get; set; }

    /// <summary>InitialEvaluation result; true if any measure is reportable.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("reportable")]
    public bool? Reportable { get; set; }

    /// <summary>Occurrence time in UTC. Producers must preserve this value across retries.</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; set; }
}
