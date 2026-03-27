using System.Threading.Channels;
using LantanaGroup.Link.Automation.Configuration;
using LantanaGroup.Link.Automation.Validation;

namespace LantanaGroup.Link.Automation.Helpers;

/// <summary>
/// Orchestrates background diagnostics monitoring during the test pipeline.
/// Uses a central monitor with pluggable probes (Loki, Kafka,
/// progress, milestones) to maintain a unified runtime picture.
/// </summary>
public class BackgroundDiagnosticsMonitor : IAsyncDisposable
{
    private readonly IAutomationOutput _output;
    private readonly LokiScraper _lokiScraper;
    private readonly KafkaErrorMonitor _kafkaMonitor;
    private readonly MilestoneValidationOrchestrator _milestoneOrchestrator;
    private readonly TimeSpan _pollInterval;
    private readonly int _expectedPatientCount;
    private readonly TestRunMonitor _monitor;
    private readonly Channel<AutomationMonitorEvent> _events = Channel.CreateUnbounded<AutomationMonitorEvent>();

    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private long _eventSequence;

    private static readonly TimeSpan StallDiagnosticsThreshold = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Indicates that a critical failure was detected (dead-letter message, failed DB record, etc.)
    /// that warrants early termination of polling loops.
    /// </summary>
    public bool HasCriticalFailure => _monitor.State.HasCriticalFailure;

    /// <summary>
    /// All Kafka error messages captured during monitoring.
    /// </summary>
    public IReadOnlyList<string> KafkaErrors => _kafkaMonitor.CapturedErrors;
    public IReadOnlyCollection<MilestoneValidationOrchestrator.Milestone> CompletedMilestones => _monitor.State.CompletedMilestones;

    public BackgroundDiagnosticsMonitor(
        IAutomationOutput output,
        LokiScraper lokiScraper,
        AutomationConfig config,
        int expectedPatientCount = 0,
        TimeSpan? pollInterval = null,
        bool forwardInternalLogsToOutput = true)
    {
        _output = output;
        _lokiScraper = lokiScraper;
        _expectedPatientCount = expectedPatientCount;

        var eventingOutput = new EventingAutomationOutput(
            output,
            message => PublishEventSync(
                AutomationMonitorEventType.LogMessage,
                MonitorIssueSeverity.Info,
                "Automation",
                message),
            forwardInternalLogsToOutput);

        _kafkaMonitor = new KafkaErrorMonitor(eventingOutput, config);
        var progressMonitor = new ProgressMonitor(eventingOutput, expectedPatientCount, lokiScraper, config);
        _milestoneOrchestrator = new MilestoneValidationOrchestrator(eventingOutput, config, expectedPatientCount);
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(5);

        var probes = new IBackgroundMonitorProbe[]
        {
            new LokiErrorProbe(_lokiScraper, _pollInterval),
            new KafkaErrorProbe(_kafkaMonitor, TimeSpan.FromSeconds(2)),
            new ProgressProbe(progressMonitor, _pollInterval),
            new MilestoneProbe(_milestoneOrchestrator, _pollInterval)
        };

        _monitor = new TestRunMonitor(
            eventingOutput,
            probes,
            StallDiagnosticsThreshold,
            DumpStallDiagnosticsAsync,
            OnMonitorEventAsync);
    }

    public IAsyncEnumerable<AutomationMonitorEvent> StreamEventsAsync(CancellationToken cancellationToken = default)
    {
        return _events.Reader.ReadAllAsync(cancellationToken);
    }

    public async Task StartAsync(string facilityId, string reportId)
    {
        await _kafkaMonitor.InitializeAsync();

        _monitor.Start(facilityId, reportId, _expectedPatientCount);

        _cts = new CancellationTokenSource();
        _monitorTask = RunMonitorLoopAsync(_cts.Token);

        await PublishEventAsync(
            AutomationMonitorEventType.RunStarted,
            MonitorIssueSeverity.Info,
            "Monitor",
            $"Background diagnostics started (polling every {_pollInterval.TotalSeconds}s)",
            new Dictionary<string, string>
            {
                ["facilityId"] = facilityId,
                ["reportId"] = reportId,
                ["pollIntervalSeconds"] = _pollInterval.TotalSeconds.ToString("F0")
            });
    }

    public async Task StopAsync()
    {
        if (_cts == null) return;

        await PublishEventAsync(
            AutomationMonitorEventType.RunStopping,
            MonitorIssueSeverity.Info,
            "Monitor",
            "Stopping background diagnostics...");

        await _cts.CancelAsync();

        if (_monitorTask != null)
        {
            try
            {
                await _monitorTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        _milestoneOrchestrator.WriteSummary();

        var kafkaMessage = _kafkaMonitor.HasErrors
            ? $"{_kafkaMonitor.CapturedErrors.Count} Kafka error/retry message(s) detected during test"
            : "No Kafka error messages detected";

        await PublishEventAsync(
            AutomationMonitorEventType.LogMessage,
            _kafkaMonitor.HasErrors ? MonitorIssueSeverity.Warning : MonitorIssueSeverity.Info,
            "Kafka",
            kafkaMessage);

        var finalMessage = _monitor.State.HasCriticalFailure
            ? "Critical failure(s) detected -- see monitor events for details"
            : "No critical failures detected";

        await PublishEventAsync(
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
        await _kafkaMonitor.DisposeAsync();
        _cts?.Dispose();
    }

    private async Task RunMonitorLoopAsync(CancellationToken ct)
    {
        await _monitor.RunCycleAsync(ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_pollInterval, ct);
                await _monitor.RunCycleAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                await PublishEventAsync(
                    AutomationMonitorEventType.MonitorLoopError,
                    MonitorIssueSeverity.Warning,
                    "Monitor",
                    $"Monitor loop error: {ex.Message}");
            }
        }

        if (!ct.IsCancellationRequested)
            await _monitor.RunCycleAsync(ct);
    }

    /// <summary>
    /// When the pipeline is stalled, scan all services for errors to identify
    /// the root cause (which is often in a different service than the stalled stage).
    /// </summary>
    private async Task DumpStallDiagnosticsAsync(string stalledStage, TimeSpan stallDuration)
    {
        await PublishEventAsync(
            AutomationMonitorEventType.StallDetected,
            MonitorIssueSeverity.Warning,
            "Monitor",
            $"STALL DETECTED -- pipeline stuck at '{stalledStage}' for {stallDuration.TotalSeconds:F0}s. Scanning all services for errors...",
            new Dictionary<string, string>
            {
                ["stage"] = stalledStage,
                ["stallSeconds"] = ((int)stallDuration.TotalSeconds).ToString()
            });

        await _lokiScraper.ScrapeAllServicesErrorSummaryAsync(TimeSpan.FromMinutes(5));
    }

    private Task OnMonitorEventAsync(AutomationMonitorEvent evt)
    {
        return PublishEventAsync(evt.Type, evt.Severity, evt.Source, evt.Message, evt.Data, evt.TimestampUtc, evt.RunId);
    }

    private void PublishEventSync(
        AutomationMonitorEventType type,
        MonitorIssueSeverity severity,
        string source,
        string message,
        IReadOnlyDictionary<string, string>? data = null)
    {
        var runId = _monitor.State.ReportId;
        var seq = Interlocked.Increment(ref _eventSequence);

        _events.Writer.TryWrite(new AutomationMonitorEvent(
            Sequence: seq,
            TimestampUtc: DateTime.UtcNow,
            RunId: runId,
            Type: type,
            Severity: severity,
            Source: source,
            Message: message,
            Data: data));
    }

    private Task PublishEventAsync(
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
            RunId: runId ?? _monitor.State.ReportId,
            Type: type,
            Severity: severity,
            Source: source,
            Message: message,
            Data: data));

        return Task.CompletedTask;
    }
}
