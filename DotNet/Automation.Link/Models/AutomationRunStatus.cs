namespace LantanaGroup.Link.Automation.Link.Models;

public enum AutomationRunStatus
{
    Queued,
    Running,
    Cancelled,
    Succeeded,
    Failed,
    LiveWindowOpen,
    ReportFinalization,
    CollectingMetrics
}

public static class AutomationRunStatusExtensions
{
    public static bool IsTerminal(this AutomationRunStatus status)
        => status is AutomationRunStatus.Succeeded
            or AutomationRunStatus.Failed
            or AutomationRunStatus.Cancelled;

    public static bool IsCancellable(this AutomationRunStatus status)
        => status is AutomationRunStatus.Queued
            or AutomationRunStatus.Running
            or AutomationRunStatus.LiveWindowOpen
            or AutomationRunStatus.ReportFinalization
            or AutomationRunStatus.CollectingMetrics;

    public static bool IsInProgress(this AutomationRunStatus status)
        => status is AutomationRunStatus.Running
            or AutomationRunStatus.LiveWindowOpen
            or AutomationRunStatus.ReportFinalization
            or AutomationRunStatus.CollectingMetrics;

    public static string ToDisplayName(this AutomationRunStatus status)
        => status switch
        {
            AutomationRunStatus.CollectingMetrics => "Collecting",
            AutomationRunStatus.ReportFinalization => "Finalizing",
            AutomationRunStatus.LiveWindowOpen => "Live window",
            _ => status.ToString()
        };
}
