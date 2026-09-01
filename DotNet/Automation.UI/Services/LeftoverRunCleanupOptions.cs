namespace Automation.UI.Services;

public sealed class LeftoverRunCleanupOptions
{
    public const string SectionName = "LeftoverRunCleanup";

    public bool Enabled { get; set; } = true;

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
}
