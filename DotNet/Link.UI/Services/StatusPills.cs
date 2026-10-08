using LantanaGroup.Link.Shared.Application.Enums;

namespace Link.UI.Services;

/// <summary>
/// One status-to-pill map. None of these classes are blue.
/// In-progress runs use the charcoal active pill. New and Scheduled stay gray.
/// </summary>
public static class StatusPills
{
    public static string ForSchedule(ScheduleStatus status) => ForScheduleName(status.ToString());

    public static string ForScheduleName(string? status)
    {
        var name = Compact(status);
        if (name == "submitted")
            return "au-badge-success";
        if (name == "endofperiod")
            return "au-badge-warning";
        if (name is "failed" or "error")
            return "au-badge-danger";
        if (name is "running" or "processing" or "queued")
            return "au-badge-active";
        return "au-badge-muted";
    }

    public static string ForRun(string? status)
    {
        var name = Compact(status);
        if (name == "succeeded")
            return "au-badge-success";
        if (name == "failed")
            return "au-badge-danger";
        if (name is "cancelled" or "canceled")
            return "au-badge-warning";
        if (name is "queued" or "running" or "livewindow" or "livewindowopen" or "finalizing" or "reportfinalization" or "collecting" or "collectingmetrics"
            || AutomationRules.IsInProgress(status))
            return "au-badge-active";
        return "au-badge-muted";
    }

    public static string ForLevel(string? level)
    {
        var name = Compact(level);
        if (name.Length == 0)
            return "au-badge-muted";
        if (name is "fatal" or "error" || name.Contains("fatal", StringComparison.Ordinal) || name.Contains("error", StringComparison.Ordinal))
            return "au-badge-danger";
        if (name.Contains("warn", StringComparison.Ordinal))
            return "au-badge-warning";
        return "au-badge-muted";
    }

    public static string ForCleanup(string? status)
    {
        var name = Compact(status);
        if (name == "running")
            return "au-badge-active";
        if (name == "failed")
            return "au-badge-danger";
        if (name is "completed" or "succeeded")
            return "au-badge-success";
        return "au-badge-muted";
    }

    public static string ForOutcome(string? outcome)
    {
        var name = Compact(outcome);
        if (name is "succeeded" or "passed" or "pass")
            return "au-badge-success";
        if (name is "failed" or "fail")
            return "au-badge-danger";
        if (name is "cancelled" or "canceled")
            return "au-badge-warning";
        return "au-badge-muted";
    }

    public static string ForMapped(bool mapped) => mapped ? "au-badge-success" : "au-badge-muted";

    public static string ForHealth(string? status)
    {
        var name = Compact(status);
        if (name is "healthy" or "ok")
            return "au-badge-success";
        if (name is "degraded" or "warning")
            return "au-badge-warning";
        if (name is "unhealthy" or "error" or "failed")
            return "au-badge-danger";
        return "au-badge-muted";
    }

    public static string ForMilestone(bool failed, bool completed) =>
        failed ? "au-badge-danger" : completed ? "au-badge-success" : "au-badge-muted";

    public static string ForCheck(bool passed, bool advisory) =>
        passed ? "au-badge-success" : advisory ? "au-badge-warning" : "au-badge-danger";

    public static string ForAccountState(bool deleted, bool active) =>
        deleted ? "au-badge-muted" : active ? "au-badge-success" : "au-badge-warning";

    public static string ForApiStep(bool stale, bool skipped, bool? passed)
    {
        if (stale)
        {
            if (skipped || passed is null)
                return "border border-secondary text-muted bg-transparent";
            return passed == true
                ? "border border-success text-success bg-transparent"
                : "border border-danger text-danger bg-transparent";
        }

        if (skipped)
            return "au-badge-skip";
        if (passed is null)
            return "au-badge-muted";
        return passed == true ? "au-badge-success" : "au-badge-danger";
    }

    public static string ForApiSummary(int failCount, bool allSkipped, int passCount, int runnableCount, bool stale)
    {
        var tone = failCount > 0
            ? "danger"
            : allSkipped
                ? "skip"
                : passCount == runnableCount && runnableCount > 0
                    ? "success"
                    : "muted";
        if (!stale)
        {
            return tone switch
            {
                "danger" => "au-badge-danger",
                "skip" => "au-badge-skip",
                "success" => "au-badge-success",
                _ => "au-badge-muted"
            };
        }

        return tone switch
        {
            "danger" => "border border-danger text-danger bg-transparent",
            "success" => "border border-success text-success bg-transparent",
            _ => "border border-secondary text-muted bg-transparent"
        };
    }

    private static string Compact(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? ""
            : value.Trim().ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");

    public static string ForLog(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return "au-badge-muted";
        if (status.Equals("Failed", StringComparison.OrdinalIgnoreCase)
            || status.Equals("MaxRetriesReached", StringComparison.OrdinalIgnoreCase)
            || status.Contains("error", StringComparison.OrdinalIgnoreCase)
            || status.Contains("fail", StringComparison.OrdinalIgnoreCase))
            return "au-badge-danger";
        if (status.Equals("Completed", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Succeeded", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Submitted", StringComparison.OrdinalIgnoreCase))
            return "au-badge-success";
        if (status.Equals("Skipped", StringComparison.OrdinalIgnoreCase))
            return "au-badge-skip";
        if (status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Canceled", StringComparison.OrdinalIgnoreCase))
            return "au-badge-warning";
        if (status.Equals("Processing", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Queued", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Pending", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Ready", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Running", StringComparison.OrdinalIgnoreCase)
            || AutomationRules.IsInProgress(status))
            return "au-badge-active";
        return "au-badge-muted";
    }

    public static string ForTerm(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return "au-badge-muted";
        if (status.Equals("active", StringComparison.OrdinalIgnoreCase))
            return "au-badge-success";
        if (status.Contains("inactiv", StringComparison.OrdinalIgnoreCase)
            || status.Contains("retir", StringComparison.OrdinalIgnoreCase))
            return "au-badge-warning";
        return ForLog(status);
    }

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
