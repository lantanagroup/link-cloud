namespace LantanaGroup.Link.Automation.Link.Helpers;

/// <summary>
/// Cadence for Automation's three independent loops. Lightweight (ordinary) runs
/// must not poll five pipeline APIs plus Loki every 5 seconds.
/// </summary>
public static class AutomationRunPollingPolicy
{
    public static readonly TimeSpan MetricsOrchestratorInterval = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan LightweightOrchestratorInterval = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan MetricsPollerInterval = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan LightweightPollerInterval = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan MetricsDiagnosticsInterval = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan LightweightDiagnosticsInterval = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan LargeRunDiagnosticsInterval = TimeSpan.FromSeconds(15);

    public static TimeSpan OrchestratorInterval(bool anyMetricsRun) =>
        anyMetricsRun ? MetricsOrchestratorInterval : LightweightOrchestratorInterval;

    public static TimeSpan PollerInterval(bool isMetricsRun) =>
        isMetricsRun ? MetricsPollerInterval : LightweightPollerInterval;

    public static TimeSpan DiagnosticsInterval(bool isMetricsRun, int patientCount)
    {
        if (!isMetricsRun)
            return LightweightDiagnosticsInterval;

        return patientCount >= 500 ? LargeRunDiagnosticsInterval : MetricsDiagnosticsInterval;
    }

    public static bool PollAllDomainsDuringRun(bool isMetricsRun) => isMetricsRun;

    public static bool ScrapeNormalizationResourceTypes(bool isMetricsRun) => isMetricsRun;
}
