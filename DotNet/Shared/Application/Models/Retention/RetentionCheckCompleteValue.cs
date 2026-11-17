using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Shared.Application.Models.Retention;

/// <summary>
/// The value of a <c>RetentionCheckComplete</c> message: one participant's final outcome for one
/// <see cref="RetentionCheckRequestedValue"/>.
/// </summary>
/// <remarks>
/// A report-scoped participant fills <see cref="Reports"/>; a participant not tied to a report fills
/// <see cref="Summary"/>. Keyed by <see cref="RetentionCleanupId"/>. See dev-docs/retention-events.md.
/// </remarks>
public class RetentionCheckCompleteValue
{
    /// <summary>
    /// Copied from the request.
    /// </summary>
    public required string RetentionCleanupId { get; set; }

    /// <summary>
    /// The participant replying, as named in Report's configured participant list.
    /// </summary>
    public required string ServiceName { get; set; }

    /// <summary>
    /// When the participant finished the request, whatever the outcome, in UTC.
    /// </summary>
    public required DateTimeOffset CompletedDateTime { get; set; }

    /// <summary>
    /// One result for every report in the request, from a report-scoped participant. Report treats a
    /// report missing from this list as failed.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<RetentionReportResult>? Reports { get; set; }

    /// <summary>
    /// The one result for the whole request, from a participant not tied to a report. Its
    /// <see cref="RetentionReportResult.ReportScheduleId"/> is absent.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RetentionReportResult? Summary { get; set; }
}
