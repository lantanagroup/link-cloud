using Xunit.Abstractions;

namespace LantanaGroup.Link.Tests.E2ETests.Helpers;

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
    private readonly DatabaseProgressMonitor _dbMonitor;
    private readonly TimeSpan _pollInterval;

    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private volatile bool _hasCriticalFailure;
    private string _facilityId = "";
    private string _reportId = "";

    /// <summary>
    /// Indicates that a critical failure was detected (dead-letter message, failed DB record, etc.)
    /// that warrants early termination of polling loops.
    /// </summary>
    public bool HasCriticalFailure => _hasCriticalFailure;

    /// <summary>
    /// All Kafka error messages captured during monitoring.
    /// </summary>
    public IReadOnlyList<string> KafkaErrors => _kafkaMonitor.CapturedErrors;

    public BackgroundDiagnosticsMonitor(ITestOutputHelper output, LokiScraper lokiScraper, TimeSpan? pollInterval = null)
    {
        _output = output;
        _lokiScraper = lokiScraper;
        _kafkaMonitor = new KafkaErrorMonitor(output);
        _dbMonitor = new DatabaseProgressMonitor(output);
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(5);
    }

    /// <summary>
    /// Starts monitoring in the background. Call this before the report generation step.
    /// </summary>
    /// <param name="facilityId">The facility being tested.</param>
    /// <param name="reportId">The report ID to track in database queries.</param>
    public async Task StartAsync(string facilityId, string reportId)
    {
        _facilityId = facilityId;
        _reportId = reportId;

        await _kafkaMonitor.InitializeAsync();

        _cts = new CancellationTokenSource();
        _monitorTask = RunMonitorLoopAsync(_cts.Token);

        _output.WriteLine($"[DIAG] Background diagnostics started (polling every {_pollInterval.TotalSeconds}s)");
    }

    /// <summary>
    /// Stops monitoring and writes a final summary.
    /// </summary>
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

        // Final summary
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
            _output.WriteLine("[DIAG] Critical failure(s) detected — see [DIAG] entries above for details");
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
        // Initial poll to establish baselines
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

        // One final check to capture anything from the last interval
        await RunSingleCheckAsync();
    }

    private async Task RunSingleCheckAsync()
    {
        // 1. Loki — errors and warnings from all services
        await _lokiScraper.ScrapeErrorsAsync();

        // 2. Loki — targeted scraping for measureeval and validation services
        await _lokiScraper.ScrapeServiceLogsAsync("measureeval", "validation");

        // 3. Kafka — listener runs on its own thread, just check for captured errors
        if (_kafkaMonitor.HasErrors)
        {
            _hasCriticalFailure = true;
        }

        // 4. Database — stuck/failed records
        if (!string.IsNullOrEmpty(_reportId))
        {
            var dbFailure = await _dbMonitor.CheckProgressAsync(_facilityId, _reportId);
            if (dbFailure)
            {
                _hasCriticalFailure = true;
            }
        }
    }
}
