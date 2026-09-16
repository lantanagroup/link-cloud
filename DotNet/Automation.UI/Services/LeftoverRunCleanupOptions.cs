namespace Automation.UI.Services;

public sealed class LeftoverRunCleanupOptions
{
    public const string SectionName = "LeftoverRunCleanup";

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Cheap daytime pass: abort leftover GUID work so DA/census stop hammering.
    /// Teardown and history purge stay on the off-hours schedule.
    /// </summary>
    public bool QuiesceEnabled { get; set; } = true;

    /// <summary>How often the sweeper quiesces hot leftover work.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Delay after process start before the first sweep.</summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Wait this long after a run becomes terminal before the sweeper quiesces it.
    /// Immediate cancel/fail/success still quiesces without waiting.
    /// </summary>
    public TimeSpan QuiesceGrace { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Keep resting facility configs, cancelled logs, and Error-topic payloads this long.
    /// </summary>
    public TimeSpan TeardownRetention { get; set; } = TimeSpan.FromDays(14);

    /// <summary>How long Kafka listeners honor an abort flag.</summary>
    public TimeSpan AbortTtl { get; set; } = TimeSpan.FromDays(14);

    /// <summary>Cap facilities handled in one pass so a huge leftover set cannot stall the host.</summary>
    public int MaxFacilitiesPerPass { get; set; } = 25;

    public bool DailyTeardownEnabled { get; set; } = true;

    /// <summary>UTC clock time for the daily 14-day leftover teardown. Default 10:00 UTC.</summary>
    public string DailyTeardownTimeUtc { get; set; } = "10:00";

    public bool WeeklyHistoryPurgeEnabled { get; set; } = true;

    public DayOfWeek WeeklyHistoryPurgeDay { get; set; } = DayOfWeek.Sunday;

    /// <summary>UTC clock time for the weekly scenario-run history purge. Default 10:00 UTC.</summary>
    public string WeeklyHistoryPurgeTimeUtc { get; set; } = "10:00";

    /// <summary>
    /// If the process was down at the scheduled time, still run if we come up inside
    /// this window. After the window, wait for the next scheduled day.
    /// </summary>
    public TimeSpan CatchUpWindow { get; set; } = TimeSpan.FromHours(3);
}
