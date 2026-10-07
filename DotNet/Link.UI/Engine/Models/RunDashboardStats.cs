namespace Automation.UI.Models;

/// <summary>
/// Aggregated run statistics for the Runs dashboard.
/// </summary>
public class RunDashboardStats
{
    public int TotalRuns { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public int Cancelled { get; set; }
    public int Running { get; set; }
    public int Queued { get; set; }

    /// <summary>Average duration (seconds) for completed runs (Succeeded/Failed).</summary>
    public double AvgDurationSeconds { get; set; }

    /// <summary>Runs per day over the last 14 days, keyed by date string (yyyy-MM-dd).</summary>
    public Dictionary<string, RunDayBucket> RunsPerDay { get; set; } = new();

    public double SuccessRate => (Succeeded + Failed) > 0
        ? Math.Round(100.0 * Succeeded / (Succeeded + Failed), 1)
        : 0;
}

public class RunDayBucket
{
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public int Cancelled { get; set; }
    public int Other { get; set; }
}
