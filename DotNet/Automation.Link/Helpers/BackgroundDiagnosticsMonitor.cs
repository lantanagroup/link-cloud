using System.Threading.Channels;
using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Link.Automation.Link.Validation;
using LantanaGroup.Link.Shared.Application.Models.Configs;

namespace LantanaGroup.Link.Automation.Link.Helpers;

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
    private readonly LantanaGroup.Automation.Helpers.TestRunMonitor _monitor;
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
    public IReadOnlyCollection<string> CompletedMilestones => _monitor.State.CompletedMilestones;

    /// <summary>
    /// Returns true if the named milestone has been reached.
    /// Milestone names match <see cref="MilestoneValidationOrchestrator.Milestone"/> enum values as strings.
    /// </summary>
    public bool HasReachedMilestone(string milestoneName)
        => _monitor.State.CompletedMilestones.Contains(milestoneName);

    /// <summary>
    /// Waits until the specified milestone is reached, a critical failure is detected,
    /// or the timeout expires. Returns true if the milestone was reached.
    /// </summary>
    public async Task<bool> WaitForMilestoneAsync(string milestoneName, TimeSpan timeout, TimeSpan? pollInterval = null)
    {
        var interval = pollInterval ?? TimeSpan.FromSeconds(2);
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (HasCriticalFailure)
                return false;

            if (HasReachedMilestone(milestoneName))
                return true;

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                break;

            await Task.Delay(remaining < interval ? remaining : interval);
        }

        return HasReachedMilestone(milestoneName);
    }

    public BackgroundDiagnosticsMonitor(
        IAutomationOutput output,
        LokiScraper lokiScraper,
        AutomationConfig config,
        KafkaConnection kafkaConnection,
        int expectedPatientCount = 0,
        TimeSpan? pollInterval = null,
        bool forwardInternalLogsToOutput = true,
        PipelineDataReader? pipelineReader = null,
        bool expectsDataAcquisition = true,
        bool scrapeNormalizationResourceTypes = true)
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

        var reader = pipelineReader ?? BuildPipelineReader(config);

        _kafkaMonitor = new KafkaErrorMonitor(
            eventingOutput,
            config,
            kafkaConnection);
        var progressMonitor = new ProgressMonitor(
            eventingOutput,
            expectedPatientCount,
            lokiScraper,
            reader,
            expectsDataAcquisition,
            scrapeNormalizationResourceTypes);
        _milestoneOrchestrator = new MilestoneValidationOrchestrator(eventingOutput, reader, expectedPatientCount, expectsDataAcquisition);
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(5);

        var probes = new IBackgroundMonitorProbe[]
        {
            new LokiErrorProbe(_lokiScraper, _pollInterval),
            new KafkaErrorProbe(_kafkaMonitor, TimeSpan.FromSeconds(2)),
            new ProgressProbe(progressMonitor, _pollInterval),
            new MilestoneProbe(_milestoneOrchestrator, _pollInterval)
        };

        _monitor = new LantanaGroup.Automation.Helpers.TestRunMonitor(
            eventingOutput,
            probes,
            StallDiagnosticsThreshold,
            DumpStallDiagnosticsAsync,
            OnMonitorEventAsync);
    }

    private static PipelineDataReader BuildPipelineReader(AutomationConfig config)
    {
        throw new InvalidOperationException(
            "Standalone client construction is no longer supported. " +
            "Pass a PipelineDataReader instance via the constructor.");
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

        // Cancel the background loop FIRST so it stops producing output
        // while the final cycle and shutdown logic run.
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

        try
        {
            await _monitor.RunCycleAsync(CancellationToken.None, forceAllProbes: true);
        }
        catch (Exception ex)
        {
            await PublishEventAsync(
                AutomationMonitorEventType.MonitorLoopError,
                MonitorIssueSeverity.Warning,
                "Monitor",
                $"Final monitor cycle error before stop: {ex.Message}");
        }

        _milestoneOrchestrator.WriteSummary();

        // Scope the stop-time Kafka summary to errors keyed to THIS test's facility, so
        // it lines up with what KafkaErrorProbe was actually counting during the run.
        // Errors with a different key are noise from sibling tests on the shared stack.
        var scopedDeadLetters = _kafkaMonitor.GetDeadLetterCountForFacility(_monitor.State.CorrelationId1);
        var scopedRetries = _kafkaMonitor.GetRetryCountForFacility(_monitor.State.CorrelationId1);
        var kafkaMessage = scopedDeadLetters + scopedRetries > 0
            ? $"{scopedDeadLetters} Kafka dead-letter and {scopedRetries} retry message(s) detected during test"
            : "No Kafka error messages detected";

        await PublishEventAsync(
            AutomationMonitorEventType.LogMessage,
            scopedDeadLetters > 0 ? MonitorIssueSeverity.Warning : MonitorIssueSeverity.Info,
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
            // Stop the loop when a critical failure has been detected — the
            // polling caller (CheckSubmissionStatusAsync) will see
            // HasCriticalFailure and exit on its own.
            if (_monitor.State.HasCriticalFailure)
                break;

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

        await _lokiScraper.ScrapeAllServicesErrorSummaryAsync(
            TimeSpan.FromMinutes(5),
            _monitor.State.CorrelationId1,
            _monitor.State.CorrelationId2);
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
        var runId = _monitor.State.CorrelationId2;
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
            RunId: runId ?? _monitor.State.CorrelationId2,
            Type: type,
            Severity: severity,
            Source: source,
            Message: message,
            Data: data));

        return Task.CompletedTask;
    }
}
