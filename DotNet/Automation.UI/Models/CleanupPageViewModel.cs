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
    public bool TeardownFacilities { get; set; } = true;
    public bool PurgeHistory { get; set; }
}
