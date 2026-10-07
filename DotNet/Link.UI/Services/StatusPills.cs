using LantanaGroup.Link.Shared.Application.Enums;

namespace Link.UI.Services;

/// <summary>
/// One status-to-pill map. None of these classes are blue.
/// In-progress runs use the charcoal active pill. New and Scheduled stay gray.
/// </summary>
public static class StatusPills
{
    public static string ForSchedule(ScheduleStatus status) => status switch
    {
        ScheduleStatus.EndOfPeriod => "au-badge-warning",
        ScheduleStatus.Submitted => "au-badge-success",
        _ => "au-badge-muted"
    };

    public static string ForRun(string? status)
    {
        if (status == "Succeeded")
            return "au-badge-success";
        if (status == "Failed")
            return "au-badge-danger";
        if (status == "Cancelled")
            return "au-badge-warning";
        if (AutomationRules.IsInProgress(status))
            return "au-badge-active";
        return "au-badge-muted";
    }

    public static string ForLog(string? status) =>
        status is "Failed" or "MaxRetriesReached" ? "au-badge-danger" : "au-badge-muted";

    public static string ForSeverity(string? severity)
    {
        if (string.IsNullOrWhiteSpace(severity))
            return "au-badge-muted";
        if (severity.Contains("error", StringComparison.OrdinalIgnoreCase)
            || severity.Contains("fatal", StringComparison.OrdinalIgnoreCase)
            || severity.Contains("unacceptable", StringComparison.OrdinalIgnoreCase))
            return "au-badge-danger";
        if (severity.Contains("warn", StringComparison.OrdinalIgnoreCase))
            return "au-badge-warning";
        return "au-badge-muted";
    }
}
