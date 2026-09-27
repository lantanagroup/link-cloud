namespace Automation.UI.Models;

/// <summary>
/// How Automation starts a report. Maps onto Report service as follows:
/// <list type="bullet">
///   <item><see cref="Adhoc"/> — Tenant AdHocReport with an explicit patient list.
///         Report writes a <c>Frequency.Adhoc</c> / <c>AdHocType.Manual</c> schedule
///         and runs data acquisition for that list.</item>
///   <item><see cref="ScheduledReport"/> — Kafka <c>ReportScheduled</c> plus census
///         admit/discharge. Live simulation is an overlay on this method only.</item>
///   <item><see cref="RegenerateReport"/> — Tenant RegenerateReport. Report creates a
///         new <c>Frequency.Adhoc</c> schedule that copies dates, types, and patients
///         from an existing schedule and skips DA (EvaluationRequested). This Automation
///         scenario first produces a Scheduled report so there is a source to copy.</item>
/// </list>
/// </summary>
public enum ReportMethod
{
    /// <summary>Immediate report for a set patient list (Report Frequency.Adhoc / Manual).</summary>
    Adhoc,

    /// <summary>Kafka ReportScheduled plus census admit/discharge over a reporting window.</summary>
    ScheduledReport,

    /// <summary>
    /// After a source schedule exists, Tenant RegenerateReport (Report Frequency.Adhoc,
    /// patients copied from the source, no new DA).
    /// </summary>
    RegenerateReport
}

/// <summary>
/// Shared report-kickoff rules. Live is valid only on <see cref="ReportMethod.ScheduledReport"/>.
/// Adhoc and Regenerated reports are both Adhoc in Report, with different entry points.
/// </summary>
public static class ReportExecution
{
    /// <summary>
    /// Non-live scheduled (and regenerate's prerequisite scheduled report) close the Kafka
    /// window this many minutes after aligned-now so end-of-period jobs fire during the test.
    /// The scenario editor JS uses the same value (<c>scheduledWindowExtraMinutes</c>).
    /// </summary>
    public const int NonLiveScheduledCloseMinutes = 2;

    public static bool IsLiveAllowed(ReportMethod method)
        => method == ReportMethod.ScheduledReport;

    /// <summary>
    /// Census/Kafka schedule kickoff. Includes regenerate's prerequisite Scheduled report
    /// (the regenerated report itself is Adhoc in Report).
    /// </summary>
    public static bool UsesCensusScheduleKickoff(ReportMethod method)
        => method is ReportMethod.ScheduledReport or ReportMethod.RegenerateReport;

    public static bool IsScheduledLike(ReportMethod method, bool isLiveSimulation)
        => UsesCensusScheduleKickoff(method)
           || (isLiveSimulation && IsLiveAllowed(method));
}
