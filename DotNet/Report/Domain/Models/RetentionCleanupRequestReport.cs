using LantanaGroup.Link.Shared.Application.Models.Retention;

namespace LantanaGroup.Link.Report.Domain.Models;

/// <summary>
/// One report the caller selected for a retention cleanup, with the window and cutoff that selected it.
/// </summary>
public record class RetentionCleanupRequestReport
{
    /// <summary>
    /// The report's <c>ReportSchedule.Id</c>.
    /// </summary>
    public required Guid ReportScheduleId { get; init; }

    /// <summary>
    /// The facility the report belongs to.
    /// </summary>
    public required string FacilityId { get; init; }

    /// <summary>
    /// The window that selected the report. Null on a single-report cleanup, which no window selected.
    /// </summary>
    public RetentionWindow? RetentionWindow { get; init; }

    /// <summary>
    /// The cutoff that selected the report, in UTC.
    /// </summary>
    public required DateTimeOffset CutoffDateTime { get; init; }
}
