using LantanaGroup.Link.Shared.Application.Enums;

namespace LantanaGroup.Link.Shared.Application.Extensions;

/// <summary>
/// Helpers for <see cref="ScheduleStatus"/> terminality, so the terminal set is defined in
/// exactly one place rather than being spelled out at each site that asks whether a report
/// has finished.
/// </summary>
public static class ScheduleStatusExtensions
{
    /// <summary>
    /// The statuses a report will not transition out of, materialized as an array so it can be
    /// used inside EF Core query predicates (translated to a SQL <c>IN</c>). Use this in
    /// queries; use <see cref="IsTerminal"/> for in-memory checks.
    /// </summary>
    /// <remarks>
    /// Cancellation is deliberately absent. It is <c>ReportSchedule.IsDeleted</c> rather than a
    /// <see cref="ScheduleStatus"/> member, so a canceled report still carries whichever status
    /// it had reached.
    /// </remarks>
    public static readonly ScheduleStatus[] TerminalStatuses =
    [
        ScheduleStatus.Submitted,
        ScheduleStatus.CompletedNotSubmitted
    ];

    /// <summary>
    /// True if the report has finished and no further processing is expected. For in-memory
    /// checks only — EF Core cannot translate this method to SQL, so use
    /// <see cref="TerminalStatuses"/> inside query predicates.
    /// </summary>
    /// <param name="status">ScheduleStatus to check</param>
    /// <returns>True if the schedule status is terminal</returns>
    public static bool IsTerminal(this ScheduleStatus status) => TerminalStatuses.Contains(status);

    /// <summary>
    /// Shown when a soft-deleted schedule is not eligible to be restored.
    /// </summary>
    public const string RestoreBlockedDetail = "Reports deleted mid flight cannot be restored.";

    /// <summary>
    /// A soft-deleted schedule can be restored only when it had not started
    /// (<see cref="ScheduleStatus.Scheduled"/>) or had finished submission
    /// (<see cref="ScheduleStatus.Submitted"/>). An aborted run stays
    /// <see cref="ScheduleStatus.New"/> or <see cref="ScheduleStatus.EndOfPeriod"/>.
    /// <see cref="ScheduleStatus.CompletedNotSubmitted"/> has finished its work but
    /// was not submitted, so it is not restorable either.
    /// </summary>
    public static bool CanRestore(this ScheduleStatus status) =>
        status is ScheduleStatus.Scheduled or ScheduleStatus.Submitted;
}
