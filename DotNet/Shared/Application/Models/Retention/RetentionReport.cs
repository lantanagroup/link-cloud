using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Shared.Application.Models.Retention;

/// <summary>
/// One report in a <see cref="RetentionCheckRequestedValue"/>, kept paired with its facility.
/// </summary>
public class RetentionReport
{
    /// <summary>
    /// The facility the report belongs to.
    /// </summary>
    public required string FacilityId { get; set; }

    /// <summary>
    /// The report's <c>ReportSchedule.Id</c>, which MeasureEval and Validation know as
    /// <c>reportTrackingId</c>.
    /// </summary>
    public required string ReportScheduleId { get; set; }

    /// <summary>
    /// The window that selected the report, for logging and audit only. Absent on a
    /// <see cref="RetentionInitiatedBy.SingleReport"/> request, because no window selected the report.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RetentionWindow? RetentionWindow { get; set; }

    /// <summary>
    /// The cutoff that selected the report, in UTC. Informational: a report-scoped participant deletes
    /// everything it holds for the report.
    /// </summary>
    public required DateTimeOffset CutoffDateTime { get; set; }
}
