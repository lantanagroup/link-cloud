namespace LantanaGroup.Link.Automation.Link.Models;

public enum AutomationRunStatus
{
    Queued,
    Running,
    Cancelled,
    Succeeded,
    Failed,
    LiveWindowOpen,
    ReportFinalization
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
            or AutomationRunStatus.ReportFinalization;

    public static bool IsInProgress(this AutomationRunStatus status)
        => status is AutomationRunStatus.Running
            or AutomationRunStatus.LiveWindowOpen
            or AutomationRunStatus.ReportFinalization;
}
