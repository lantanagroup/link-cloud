namespace LantanaGroup.Automation.Helpers;

/// <summary>
/// Generic test run monitor that orchestrates pluggable probes, detects stalls,
/// and emits events. Platform-agnostic — the probes themselves carry the
/// domain-specific logic.
/// </summary>
public sealed class TestRunMonitor
{
    private readonly IAutomationOutput _output;
    private readonly List<IBackgroundMonitorProbe> _probes;
    private readonly Dictionary<string, DateTime> _lastProbeRunUtc = new(StringComparer.Ordinal);
    private readonly HashSet<string> _emittedIssueKeys = [];
    private readonly TimeSpan _stallThreshold;
    private readonly Func<string, TimeSpan, Task>? _onStallDetected;
    private readonly Func<AutomationMonitorEvent, Task>? _onEvent;

    private bool _stallDiagnosticsDumped;
    private bool _criticalEventEmitted;

    public TestMonitorState State { get; } = new();

    public TestRunMonitor(
        IAutomationOutput output,
        IEnumerable<IBackgroundMonitorProbe> probes,
        TimeSpan stallThreshold,
        Func<string, TimeSpan, Task>? onStallDetected = null,
        Func<AutomationMonitorEvent, Task>? onEvent = null)
    {
        _output = output;
        _probes = probes.ToList();
        _stallThreshold = stallThreshold;
        _onStallDetected = onStallDetected;
        _onEvent = onEvent;
    }

    public void Start(string correlationId1, string correlationId2, int expectedItemCount)
    {
        State.Start(correlationId1, correlationId2, expectedItemCount);
        _lastProbeRunUtc.Clear();
        _emittedIssueKeys.Clear();
        _stallDiagnosticsDumped = false;
        _criticalEventEmitted = false;
    }

    public async Task RunCycleAsync(CancellationToken cancellationToken, bool forceAllProbes = false)
    {
        State.IncrementCycle();

        var now = DateTime.UtcNow;

        await EmitEventAsync(
            AutomationMonitorEventType.CycleStarted,
            MonitorIssueSeverity.Info,
            "TestRunMonitor",
            $"Starting monitor cycle {State.CycleCount}",
            new Dictionary<string, string>
            {
                ["cycle"] = State.CycleCount.ToString(),
                ["runId"] = State.CorrelationId2
            });

        foreach (var probe in _probes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!forceAllProbes &&
                _lastProbeRunUtc.TryGetValue(probe.Name, out var lastRun) &&
                now - lastRun < probe.Interval)
            {
                continue;
            }

            _lastProbeRunUtc[probe.Name] = now;

            MonitorProbeResult result;
            try
            {
                result = await probe.ExecuteAsync(State, cancellationToken);
            }
            catch (Exception ex)
            {
                result = new MonitorProbeResult
                {
                    Issues =
                    [
                        new MonitorIssue(
                            Key: $"probe-exception-{probe.Name}",
                            Source: probe.Name,
                            Message: $"Probe execution failed: {ex.Message}",
                            Severity: MonitorIssueSeverity.Warning,
                            TimestampUtc: DateTime.UtcNow)
                    ]
                };
            }

            await MergeResultAsync(result);
        }

        if (!_stallDiagnosticsDumped &&
            State.StallDuration > _stallThreshold &&
            !string.IsNullOrWhiteSpace(State.StalledStage))
        {
            _stallDiagnosticsDumped = true;

            await EmitEventAsync(
                AutomationMonitorEventType.StallDetected,
                MonitorIssueSeverity.Warning,
                "TestRunMonitor",
                $"Pipeline stalled at '{State.StalledStage}' for {State.StallDuration.TotalSeconds:F0}s",
                new Dictionary<string, string>
                {
                    ["stage"] = State.StalledStage!,
                    ["stallSeconds"] = ((int)State.StallDuration.TotalSeconds).ToString()
                });

            if (_onStallDetected != null)
            {
                await _onStallDetected(State.StalledStage!, State.StallDuration);
            }
        }
    }

    private async Task MergeResultAsync(MonitorProbeResult result)
    {
        if (result.HasCriticalFailure == true)
            State.HasCriticalFailure = true;

        if (result.StallDuration.HasValue)
            State.StallDuration = result.StallDuration.Value;

        if (result.StalledStage != null)
            State.StalledStage = result.StalledStage;

        if (result.MessageBusErrorCount.HasValue)
            State.MessageBusErrorCount = result.MessageBusErrorCount.Value;

        if (result.CompletedMilestones != null)
        {
            var existing = State.CompletedMilestones.ToHashSet();
            State.MergeMilestones(result.CompletedMilestones);

            foreach (var milestone in result.CompletedMilestones.Where(m => !existing.Contains(m)))
            {
                await EmitEventAsync(
                    AutomationMonitorEventType.MilestoneReached,
                    MonitorIssueSeverity.Info,
                    "Milestones",
                    $"Reached milestone {milestone}",
                    new Dictionary<string, string> { ["milestone"] = milestone });
            }
        }

        foreach (var issue in result.Issues)
        {
            if (!_emittedIssueKeys.Add(issue.Key))
                continue;

            State.AddIssue(issue);
            _output.WriteLine($"[DIAG][Monitor][{issue.Source}][{issue.Severity}] {issue.Message}");

            await EmitEventAsync(
                AutomationMonitorEventType.IssueDetected,
                issue.Severity,
                issue.Source,
                issue.Message,
                new Dictionary<string, string> { ["issueKey"] = issue.Key });

            if (issue.Severity == MonitorIssueSeverity.Critical)
                State.HasCriticalFailure = true;
        }

        if (State.HasCriticalFailure && !_criticalEventEmitted)
        {
            _criticalEventEmitted = true;
            await EmitEventAsync(
                AutomationMonitorEventType.CriticalFailureDetected,
                MonitorIssueSeverity.Critical,
                "TestRunMonitor",
                "Critical failure detected in monitor state.");
        }
    }

    private Task EmitEventAsync(
        AutomationMonitorEventType type,
        MonitorIssueSeverity severity,
        string source,
        string message,
        IReadOnlyDictionary<string, string>? data = null)
    {
        if (_onEvent == null)
            return Task.CompletedTask;

        var evt = new AutomationMonitorEvent(
            Sequence: 0,
            TimestampUtc: DateTime.UtcNow,
            RunId: State.CorrelationId2,
            Type: type,
            Severity: severity,
            Source: source,
            Message: message,
            Data: data);

        return _onEvent(evt);
    }
}
