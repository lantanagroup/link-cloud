using LantanaGroup.Automation.Helpers;

namespace LantanaGroup.Link.Automation.Link.Helpers;

public sealed record MonitoredService(
    string Name,
    Func<CancellationToken, Task<bool?>> CheckHealth);

public sealed class ServiceHealthProbe : IBackgroundMonitorProbe
{
    private sealed class ServiceState
    {
        public required MonitoredService Service { get; init; }
        public bool HasBeenHealthy { get; set; }
        public DateTimeOffset? UnhealthySince { get; set; }
    }

    private readonly List<ServiceState> _services;
    private readonly TimeProvider _time;
    private readonly TimeSpan _outageThreshold;

    public ServiceHealthProbe(
        IEnumerable<MonitoredService> services,
        TimeSpan interval,
        TimeSpan outageThreshold,
        TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval));

        if (outageThreshold <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(outageThreshold));

        _services = services.Select(service =>
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentException.ThrowIfNullOrWhiteSpace(service.Name);
            ArgumentNullException.ThrowIfNull(service.CheckHealth);

            return new ServiceState
            {
                Service = service
            };
        }).ToList();

        if (_services.Count == 0)
            throw new ArgumentException(
                "At least one monitored service is required.",
                nameof(services));

        if (_services.Select(s => s.Service.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase).Count() != _services.Count)
        {
            throw new ArgumentException(
                "Monitored service names must be unique.",
                nameof(services));
        }

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
        var issues = new List<MonitorIssue>();
        var hasCriticalFailure = false;

        foreach (var serviceState in _services)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var service = serviceState.Service;

            // true = healthy, false = unhealthy, null = unknown
            var healthy = await service.CheckHealth(cancellationToken);
            var now = _time.GetUtcNow();

            if (healthy == true)
            {
                if (serviceState.UnhealthySince.HasValue)
                {
                    var duration = now - serviceState.UnhealthySince.Value;

                    issues.Add(new MonitorIssue(
                        Key: $"service-health-recovered:{service.Name}",
                        Source: Name,
                        Message:
                            $"{service.Name} recovered after " +
                            $"{duration.TotalSeconds:F0} seconds.",
                        Severity: MonitorIssueSeverity.Info,
                        TimestampUtc: now.UtcDateTime));
                }

                serviceState.HasBeenHealthy = true;
                serviceState.UnhealthySince = null;
                continue;
            }

            if (healthy == null)
            {
                // Unknown health must not be treated as proof of an outage.
                serviceState.UnhealthySince = null;
                continue;
            }

            // Do not count an outage until the service has been
            // successfully reached at least once during this run.
            if (!serviceState.HasBeenHealthy)
                continue;

            if (!serviceState.UnhealthySince.HasValue)
            {
                serviceState.UnhealthySince = now;

                issues.Add(new MonitorIssue(
                    Key: $"service-health-outage:{service.Name}",
                    Source: Name,
                    Message:
                        $"{service.Name} health check failed; " +
                        "monitoring outage duration.",
                    Severity: MonitorIssueSeverity.Warning,
                    TimestampUtc: now.UtcDateTime));

                continue;
            }

            var outageDuration = now - serviceState.UnhealthySince.Value;

            if (outageDuration < _outageThreshold)
                continue;

            hasCriticalFailure = true;

            issues.Add(new MonitorIssue(
                Key: $"service-health-timeout:{service.Name}",
                Source: Name,
                Message:
                    $"{service.Name} unavailable for " +
                    $"{outageDuration.TotalSeconds:F0} seconds; " +
                    "automation run aborted.",
                Severity: MonitorIssueSeverity.Critical,
                TimestampUtc: now.UtcDateTime));
        }

        return new MonitorProbeResult
        {
            HasCriticalFailure = hasCriticalFailure,
            Issues = issues
        };
    }
}