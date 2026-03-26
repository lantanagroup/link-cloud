using LantanaGroup.Link.Automation.Configuration;
using Xunit.Abstractions;

namespace LantanaGroup.Link.Automation.Helpers;

/// <summary>
/// Orchestrates background diagnostics monitoring during the smoke test pipeline.
/// Periodically polls Loki logs, Kafka error topics, and database state to surface
/// issues in real-time rather than waiting for a polling timeout.
/// </summary>
public class BackgroundDiagnosticsMonitor : IAsyncDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly LokiScraper _lokiScraper;
    private readonly KafkaErrorMonitor _kafkaMonitor;
    private readonly ProgressMonitor _progressMonitor;
    private readonly TimeSpan _pollInterval;

    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private volatile bool _hasCriticalFailure;
    private string _facilityId = "";
    private string _reportId = "";
    private int _cycleCount;
    private bool _stallDiagnosticsDumped;

    private static readonly TimeSpan StallDiagnosticsThreshold = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Indicates that a critical failure was detected (dead-letter message, failed DB record, etc.)
    /// that warrants early termination of polling loops.
    /// </summary>
    public bool HasCriticalFailure => _hasCriticalFailure;

    /// <summary>
    /// All Kafka error messages captured during monitoring.
    /// </summary>
    public IReadOnlyList<string> KafkaErrors => _kafkaMonitor.CapturedErrors;

    public BackgroundDiagnosticsMonitor(
        ITestOutputHelper output,
        LokiScraper lokiScraper,
        AutomationConfig config,
        int expectedPatientCount = 0,
        TimeSpan? pollInterval = null)
    {
        _output = output;
        _lokiScraper = lokiScraper;
        _kafkaMonitor = new KafkaErrorMonitor(output, config);
        _progressMonitor = new ProgressMonitor(output, expectedPatientCount, lokiScraper, config);
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(5);
    }

    public async Task StartAsync(string facilityId, string reportId)
    {
        _facilityId = facilityId;
        _reportId = reportId;

        await _kafkaMonitor.InitializeAsync();

        _cts = new CancellationTokenSource();
        _monitorTask = RunMonitorLoopAsync(_cts.Token);

        _output.WriteLine($"[DIAG] Background diagnostics started (polling every {_pollInterval.TotalSeconds}s)");
    }

    public async Task StopAsync()
    {
        if (_cts == null) return;

        _output.WriteLine("[DIAG] Stopping background diagnostics...");

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

        if (_kafkaMonitor.HasErrors)
        {
            _output.WriteLine($"[DIAG] {_kafkaMonitor.CapturedErrors.Count} Kafka error/retry message(s) detected during test");
        }
        else
        {
            _output.WriteLine("[DIAG] No Kafka error messages detected");
        }

        if (_hasCriticalFailure)
        {
            _output.WriteLine("[DIAG] Critical failure(s) detected -- see [DIAG] entries above for details");
        }
        else
        {
            _output.WriteLine("[DIAG] No critical failures detected");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        await _kafkaMonitor.DisposeAsync();
        _cts?.Dispose();
    }

    private async Task RunMonitorLoopAsync(CancellationToken ct)
    {
        await RunSingleCheckAsync();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_pollInterval, ct);
                await RunSingleCheckAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _output.WriteLine($"[DIAG] Monitor loop error: {ex.Message}");
            }
        }

        await RunSingleCheckAsync();
    }

    private async Task RunSingleCheckAsync()
    {
        _cycleCount++;

        // 1. Loki -- errors from all services (single query, only prints if something found)
        await _lokiScraper.ScrapeErrorsAsync();

        // 2. Kafka -- listener runs on its own thread, just check for captured errors
        if (_kafkaMonitor.HasErrors)
        {
            _hasCriticalFailure = true;
        }

        // 3. Progress monitor -- database state, progress bar, heartbeat, service activity
        if (!string.IsNullOrEmpty(_reportId))
        {
            var dbFailure = await _progressMonitor.CheckProgressAsync(_facilityId, _reportId);
            if (dbFailure)
            {
                _hasCriticalFailure = true;
            }
        }

        // 4. Stall detection -- if progress hasn't moved for a while, scan all services (once)
        if (!_stallDiagnosticsDumped &&
            _progressMonitor.StallDuration > StallDiagnosticsThreshold &&
            _progressMonitor.StalledStage != null)
        {
            _stallDiagnosticsDumped = true;
            await DumpStallDiagnosticsAsync(_progressMonitor.StalledStage);
        }
    }

    /// <summary>
    /// When the pipeline is stalled, scan all services for errors to identify
    /// the root cause (which is often in a different service than the stalled stage).
    /// </summary>
    private async Task DumpStallDiagnosticsAsync(string stalledStage)
    {
        _output.WriteLine($"[DIAG] STALL DETECTED -- pipeline stuck at '{stalledStage}' for {_progressMonitor.StallDuration.TotalSeconds:F0}s. Scanning all services for errors...");
        await _lokiScraper.ScrapeAllServicesErrorSummaryAsync(TimeSpan.FromMinutes(5));
    }
}
