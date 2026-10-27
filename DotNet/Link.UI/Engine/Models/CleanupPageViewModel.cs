using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using LantanaGroup.Link.Automation.Link.Helpers;

namespace Automation.UI.Models;

public sealed class CleanupPageViewModel
{
    public LeftoverRunCleanupSettings Settings { get; set; } = new();
    public DateTimeOffset NowUtc { get; set; }
    public DateTimeOffset? LastQuiesceAt { get; set; }
    public string? LastQuiesceResult { get; set; }

    public DateTimeOffset NextDailyTeardownUtc =>
        CleanupSchedule.NextDailyUtc(NowUtc, Settings.DailyTeardownTimeUtc);

    public DateTimeOffset NextWeeklyPurgeUtc =>
        CleanupSchedule.NextWeeklyUtc(NowUtc, Settings.WeeklyHistoryPurgeDay, Settings.WeeklyHistoryPurgeTimeUtc);

    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public CleanupActivity CurrentActivity { get; set; } = CleanupActivity.Idle;
    public IReadOnlyList<CleanupReport> RecentReports { get; set; } = [];
}

public sealed class CleanupSettingsForm
{
    public bool Enabled { get; set; }
    public bool QuiesceEnabled { get; set; }
    public int QuiesceIntervalMinutes { get; set; } = 5;
    public int QuiesceGraceMinutes { get; set; } = 2;
    public int TeardownRetentionDays { get; set; } = 14;
    public int AbortTtlDays { get; set; } = 14;
    public int MaxFacilitiesPerPass { get; set; } = 25;
    public bool DailyTeardownEnabled { get; set; }
    public string DailyTeardownTimeUtc { get; set; } = "10:00";
    public bool WeeklyHistoryPurgeEnabled { get; set; }
    public DayOfWeek WeeklyHistoryPurgeDay { get; set; } = DayOfWeek.Sunday;
    public string WeeklyHistoryPurgeTimeUtc { get; set; } = "10:00";
    public int CatchUpWindowHours { get; set; } = 3;
}

public sealed class CleanupCustomRangeForm
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public bool TeardownFacilities { get; set; }
    public bool PurgeHistory { get; set; }
}

/// <summary>
/// Bootstrap 5.1 background classes for Cleanup status text.
/// This UI ships Bootstrap 5.1, which has no <c>text-bg-*</c> utilities, so a badge
/// that only names those classes is white text on the row and cannot be read.
/// </summary>
public static class CleanupStatusBadge
{
    public const string Failed = "bg-danger";
    public const string Completed = "bg-success";
    public const string Running = "bg-primary";
    public const string Idle = "bg-secondary";

    public const string FailedLabel = "Failed";
    public const string CompletedLabel = "Completed";
    public const string RunningLabel = "Running";
    public const string IdleLabel = "Idle";

    public static string ForReport(string? status)
        => string.Equals(status, "failed", StringComparison.Ordinal) ? Failed : Completed;

    public static string ForActivity(string? status) => status switch
    {
        "running" => Running,
        "completed" => Completed,
        "failed" => Failed,
        _ => Idle
    };

    public static string ActivityLabel(string? status) => status switch
    {
        "running" => RunningLabel,
        "completed" => CompletedLabel,
        "failed" => FailedLabel,
        _ => IdleLabel
    };
}
