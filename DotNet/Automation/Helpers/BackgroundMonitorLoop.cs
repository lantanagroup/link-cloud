using System.Threading.Channels;

namespace LantanaGroup.Automation.Helpers;

/// <summary>
/// Platform-agnostic background monitoring loop that orchestrates a
/// <see cref="TestRunMonitor"/> with pluggable probes on a configurable
/// polling interval and exposes a <see cref="Channel{T}"/> of events.
///
/// Platform-specific code composes this by supplying probes, diagnostics
/// callbacks, and an optional message bus monitor.
/// </summary>
public class BackgroundMonitorLoop : IAsyncDisposable
{
    private readonly IAutomationOutput _output;
    private readonly TestRunMonitor _monitor;
    private readonly IMessageBusMonitor? _messageBusMonitor;
    private readonly TimeSpan _pollInterval;
    private readonly Channel<AutomationMonitorEvent> _events = Channel.CreateUnbounded<AutomationMonitorEvent>();

    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private long _eventSequence;

    /// <summary>
    /// Indicates that a critical failure was detected that warrants
    /// early termination of polling loops.
    /// </summary>
    public bool HasCriticalFailure => _monitor.State.HasCriticalFailure;

    /// <summary>
    /// Current monitor state (milestones, issues, stall info).
    /// </summary>
    public TestMonitorState State => _monitor.State;

    /// <summary>
    /// Error messages captured by the message bus monitor, if one was supplied.
    /// </summary>
    public IReadOnlyList<string> MessageBusErrors => _messageBusMonitor?.CapturedErrors ?? [];

    /// <summary>
    /// Completed milestones as reported by the probes.
    /// </summary>
    public IReadOnlyCollection<string> CompletedMilestones => _monitor.State.CompletedMilestones;

    public BackgroundMonitorLoop(
        IAutomationOutput output,
        IEnumerable<IBackgroundMonitorProbe> probes,
        TimeSpan stallThreshold,
        TimeSpan? pollInterval = null,
        IMessageBusMonitor? messageBusMonitor = null,
        Func<string, TimeSpan, Task>? onStallDetected = null)
    {
        _output = output;
        _messageBusMonitor = messageBusMonitor;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(5);

        _monitor = new TestRunMonitor(
            output,
            probes,
            stallThreshold,
            onStallDetected,
            OnMonitorEventAsync);
    }

    /// <summary>
    /// Streams all monitor events as an async enumerable.
    /// </summary>
    public IAsyncEnumerable<AutomationMonitorEvent> StreamEventsAsync(CancellationToken cancellationToken = default)
        => _events.Reader.ReadAllAsync(cancellationToken);

    /// <summary>
    /// Starts the background monitor loop.
    /// </summary>
    /// <param name="correlationId1">Primary correlation identifier (e.g., facility ID).</param>
    /// <param name="correlationId2">Secondary correlation identifier (e.g., report/run ID).</param>
    /// <param name="expectedItemCount">Expected item count for progress tracking.</param>
    public async Task StartAsync(string correlationId1, string correlationId2, int expectedItemCount = 0)
    {
        if (_messageBusMonitor != null)
            await _messageBusMonitor.InitializeAsync();

        _monitor.Start(correlationId1, correlationId2, expectedItemCount);

        _cts = new CancellationTokenSource();
        _monitorTask = RunLoopAsync(_cts.Token);

        PublishEvent(
            AutomationMonitorEventType.RunStarted,
            MonitorIssueSeverity.Info,
            "Monitor",
            $"Background monitoring started (polling every {_pollInterval.TotalSeconds}s)",
            new Dictionary<string, string>
            {
                ["correlationId1"] = correlationId1,
                ["correlationId2"] = correlationId2,
                ["pollIntervalSeconds"] = _pollInterval.TotalSeconds.ToString("F0")
            });
    }

    /// <summary>
    /// Stops the background monitor loop and runs a final probe cycle.
    /// </summary>
    public async Task StopAsync()
    {
        if (_cts == null) return;

        try
        {
            await _monitor.RunCycleAsync(CancellationToken.None, forceAllProbes: true);
        }
        catch (Exception ex)
        {
            PublishEvent(
                AutomationMonitorEventType.MonitorLoopError,
                MonitorIssueSeverity.Warning,
                "Monitor",
                $"Final monitor cycle error before stop: {ex.Message}");
        }

        PublishEvent(
            AutomationMonitorEventType.RunStopping,
            MonitorIssueSeverity.Info,
            "Monitor",
            "Stopping background monitoring...");

        await _cts.CancelAsync();

        if (_monitorTask != null)
        {
            try { await _monitorTask; }
            catch (OperationCanceledException) { }
        }

        if (_messageBusMonitor != null)
        {
            var busMessage = _messageBusMonitor.HasErrors
                ? $"{_messageBusMonitor.CapturedErrors.Count} message bus error/retry message(s) detected during test"
                : "No message bus error messages detected";

            PublishEvent(
                AutomationMonitorEventType.LogMessage,
                _messageBusMonitor.HasErrors ? MonitorIssueSeverity.Warning : MonitorIssueSeverity.Info,
                "MessageBus",
                busMessage);
        }

        var finalMessage = _monitor.State.HasCriticalFailure
            ? "Critical failure(s) detected -- see monitor events for details"
            : "No critical failures detected";

        PublishEvent(
            AutomationMonitorEventType.RunStopped,
            _monitor.State.HasCriticalFailure ? MonitorIssueSeverity.Critical : MonitorIssueSeverity.Info,
            "Monitor",
            finalMessage,
            new Dictionary<string, string>
            {
                ["critical"] = _monitor.State.HasCriticalFailure.ToString(),
                ["issues"] = _monitor.State.Issues.Count.ToString()
            });
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();

        if (_messageBusMonitor != null)
            await _messageBusMonitor.DisposeAsync();

        _cts?.Dispose();
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        await _monitor.RunCycleAsync(ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_pollInterval, ct);
                await _monitor.RunCycleAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                PublishEvent(
                    AutomationMonitorEventType.MonitorLoopError,
                    MonitorIssueSeverity.Warning,
                    "Monitor",
                    $"Monitor loop error: {ex.Message}");
            }
        }

        if (!ct.IsCancellationRequested)
            await _monitor.RunCycleAsync(ct);
    }

    private Task OnMonitorEventAsync(AutomationMonitorEvent evt)
    {
        PublishEvent(evt.Type, evt.Severity, evt.Source, evt.Message, evt.Data, evt.TimestampUtc, evt.RunId);
        return Task.CompletedTask;
    }

    private void PublishEvent(
        AutomationMonitorEventType type,
        MonitorIssueSeverity severity,
        string source,
        string message,
        IReadOnlyDictionary<string, string>? data = null,
        DateTime? timestampUtc = null,
        string? runId = null)
    {
        var seq = Interlocked.Increment(ref _eventSequence);

        _events.Writer.TryWrite(new AutomationMonitorEvent(
            Sequence: seq,
            TimestampUtc: timestampUtc ?? DateTime.UtcNow,
            RunId: runId ?? _monitor.State.CorrelationId2,
            Type: type,
            Severity: severity,
            Source: source,
            Message: message,
            Data: data));
    }
}
