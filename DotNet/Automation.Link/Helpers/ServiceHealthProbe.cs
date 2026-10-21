using LantanaGroup.Automation.Helpers;

namespace LantanaGroup.Link.Automation.Link.Helpers;

public sealed class ServiceHealthProbe : IBackgroundMonitorProbe
{
    private readonly Func<CancellationToken, Task<bool?>> _checkHealth;
    private readonly TimeProvider _time;
    private readonly TimeSpan _outageThreshold;
    private DateTimeOffset? _unhealthySince;

    public ServiceHealthProbe(
        Func<CancellationToken, Task<bool?>> checkHealth,
        TimeSpan interval,
        TimeSpan outageThreshold,
        TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(checkHealth);

        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval));

        if (outageThreshold <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(outageThreshold));

        _checkHealth = checkHealth;
        Interval = interval;
        _outageThreshold = outageThreshold;
        _time = time ?? TimeProvider.System;
    }

    public string Name => "ServiceHealth";

    public TimeSpan Interval { get; }

    public async Task<MonitorProbeResult> ExecuteAsync(
        TestMonitorState state,
        CancellationToken cancellationToken)
    {
        // true = healthy, false = unhealthy, null = unknown
        var healthy = await _checkHealth(cancellationToken);
        var now = _time.GetUtcNow();

        if (healthy == true)
        {
            _unhealthySince = null;
            return MonitorProbeResult.Empty;
        }

        if (healthy == null)
        {
            // Do not treat unavailable monitoring as proof of outage.
            _unhealthySince = null;
            return MonitorProbeResult.Empty;
        }

        _unhealthySince ??= now;

        var outageDuration = now - _unhealthySince.Value;

        if (outageDuration < _outageThreshold)
            return MonitorProbeResult.Empty;

        return new MonitorProbeResult
        {
            HasCriticalFailure = true,
            Issues =
            [
                new MonitorIssue(
                    Key: "service-health-timeout",
                    Source: Name,
                    Message:
                        $"Required upstream service has remained unhealthy " +
                        $"for {outageDuration.TotalSeconds:F0} seconds.",
                    Severity: MonitorIssueSeverity.Critical,
                    TimestampUtc: now.UtcDateTime)
            ]
        };
    }
}