namespace Automation.UI.Models;

/// <summary>
/// How the report is triggered during the test run.
/// </summary>
public enum ReportMethod
{
    /// <summary>Ad-hoc report generation (immediate).</summary>
    Adhoc,

    /// <summary>Scheduled report via Kafka ReportScheduled event with patient admit/discharge.</summary>
    ScheduledReport,

    /// <summary>Regenerate an existing submitted report.</summary>
    RegenerateReport
}
