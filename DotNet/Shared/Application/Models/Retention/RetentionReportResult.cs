using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Shared.Application.Models.Retention;

/// <summary>
/// A participant's outcome for one report, or for a whole request when used as
/// <see cref="RetentionCheckCompleteValue.Summary"/>.
/// </summary>
public class RetentionReportResult
{
    /// <summary>
    /// Copied from the request, so Report can match the result to its report. Required in every
    /// <see cref="RetentionCheckCompleteValue.Reports"/> entry; absent in the summary.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReportScheduleId { get; set; }

    /// <summary>
    /// Whether the participant finished the report.
    /// </summary>
    public required RetentionResultStatus Status { get; set; }

    /// <summary>
    /// Records deleted on this attempt. On a failure, the count deleted before it; 0 when there was
    /// nothing left to delete.
    /// </summary>
    public required long RecordsAffected { get; set; }

    /// <summary>
    /// The participant's own description of a failure and the exception type. Never the raw exception
    /// message, and never PHI; the full exception goes to the participant's logs.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// When the participant finished the report, whatever the outcome, in UTC.
    /// </summary>
    public required DateTimeOffset CompletedDateTime { get; set; }
}
