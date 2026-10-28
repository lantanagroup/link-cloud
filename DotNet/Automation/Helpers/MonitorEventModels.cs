namespace LantanaGroup.Automation.Helpers;

public enum AutomationMonitorEventType
{
    RunStarted,
    RunStopping,
    RunStopped,
    CycleStarted,
    LogMessage,
    MilestoneReached,
    IssueDetected,
    CriticalFailureDetected,
    StallDetected,
    MonitorLoopError
}

public sealed record AutomationMonitorEvent(
    long Sequence,
    DateTime TimestampUtc,
    string RunId,
    AutomationMonitorEventType Type,
    MonitorIssueSeverity Severity,
    string Source,
    string Message,
    IReadOnlyDictionary<string, string>? Data = null);
