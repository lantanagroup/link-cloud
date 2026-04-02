using System.Collections.Concurrent;
using Automation.UI.Services.Persistence;
using LantanaGroup.Link.Automation.Helpers;

namespace Automation.UI.Services;

/// <summary>
/// Long-running background service that manages per-run data pollers.
/// Periodically checks for active runs and ensures each one has a poller.
/// When a run completes, its poller is stopped and removed.
///
/// This is the single place that does API polling — the UI controllers
/// only read from <see cref="ISnapshotStore"/>.
/// </summary>
public sealed class RunSnapshotOrchestrator : BackgroundService
{
    private readonly ISnapshotStore _store;
    private readonly IServiceProvider _services;
    private readonly ILogger<RunSnapshotOrchestrator> _logger;
    private readonly ConcurrentDictionary<Guid, RunPollerHandle> _activePollers = new();

    private static readonly TimeSpan OrchestrationInterval = TimeSpan.FromSeconds(2);

    public RunSnapshotOrchestrator(
        ISnapshotStore store,
        IServiceProvider services,
        ILogger<RunSnapshotOrchestrator> logger)
    {
        _store = store;
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RunSnapshotOrchestrator started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Orchestrator reconciliation error");
            }

            try
            {
                await Task.Delay(OrchestrationInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        // Shutdown: stop all pollers
        await StopAllPollersAsync();
        _logger.LogInformation("RunSnapshotOrchestrator stopped");
    }

    /// <summary>
    /// Registers a new run so the orchestrator will start polling for it.
    /// Called by <see cref="AutomationRunManager"/> when facility + report are known.
    /// </summary>
    public async Task RegisterRunAsync(Guid runId, string facilityId, string reportId)
    {
        var meta = new RunSnapshotMeta
        {
            RunId = runId,
            FacilityId = facilityId,
            ReportId = reportId,
            StartedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };

        await _store.RegisterRunAsync(runId, meta);
        _logger.LogInformation("Registered run {RunId} for snapshot polling", runId);
    }

    /// <summary>
    /// Marks a run as complete so the orchestrator stops polling.
    /// </summary>
    public async Task CompleteRunAsync(Guid runId)
    {
        // Do a final domain-data flush BEFORE stopping the poller,
        // so the last state of every domain is guaranteed to be persisted.
        if (_activePollers.TryGetValue(runId, out var activeHandle))
        {
            try
            {
                await activeHandle.FinalPollAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Final poll for run {RunId} failed; domain data may be stale", runId);
            }
        }

        await _store.CompleteRunAsync(runId);

        if (_activePollers.TryRemove(runId, out var handle))
        {
            await handle.StopAsync();
            _logger.LogInformation("Stopped poller for completed run {RunId}", runId);
        }
    }

    private async Task ReconcileAsync(CancellationToken ct)
    {
        var activeRuns = await _store.GetActiveRunsAsync(ct);
        var activeRunIds = activeRuns.Select(r => r.RunId).ToHashSet();

        // Start pollers for runs that don't have one yet
        foreach (var meta in activeRuns)
        {
            if (_activePollers.TryGetValue(meta.RunId, out var existingHandle) && existingHandle.IsCompleted)
            {
                if (_activePollers.TryRemove(meta.RunId, out var completedHandle))
                {
                    await completedHandle.StopAsync();
                    _logger.LogWarning("Restarting completed poller task for still-active run {RunId}", meta.RunId);
                }
            }

            if (!_activePollers.ContainsKey(meta.RunId))
            {
                if (string.IsNullOrWhiteSpace(meta.FacilityId) || string.IsNullOrWhiteSpace(meta.ReportId))
                {
                    _logger.LogDebug("Skipping poller start for run {RunId}: missing facility/report identifiers", meta.RunId);
                    continue;
                }

                StartPoller(meta, ct);
            }
        }

        // Stop pollers for runs no longer active
        foreach (var (runId, handle) in _activePollers)
        {
            if (!activeRunIds.Contains(runId))
            {
                if (_activePollers.TryRemove(runId, out var removed))
                {
                    await removed.StopAsync();
                    _logger.LogInformation("Removed stale poller for run {RunId}", runId);
                }
            }
        }
    }

    private void StartPoller(RunSnapshotMeta meta, CancellationToken ct)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // Build a scoped service provider for the poller's API clients
        var scope = _services.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<PipelineDataReader>();
        var poller = new StoreBackedServicePoller(_store, reader, meta, _logger);

        var task = poller.RunAsync(cts.Token);
        var handle = new RunPollerHandle(cts, task, scope, poller);

        if (_activePollers.TryAdd(meta.RunId, handle))
        {
            _logger.LogInformation("Started poller for run {RunId} (facility={FacilityId})", meta.RunId, meta.FacilityId);
        }
        else
        {
            cts.Cancel();
            scope.Dispose();
        }
    }

    private async Task StopAllPollersAsync()
    {
        var tasks = new List<Task>();
        foreach (var (_, handle) in _activePollers)
        {
            tasks.Add(handle.StopAsync());
        }

        await Task.WhenAll(tasks);
        _activePollers.Clear();
    }

    private sealed class RunPollerHandle(
        CancellationTokenSource cts,
        Task pollerTask,
        IServiceScope scope,
        StoreBackedServicePoller poller)
    {
        public bool IsCompleted => pollerTask.IsCompleted;

        public Task FinalPollAsync() => poller.FinalPollAsync();

        public async Task StopAsync()
        {
            await cts.CancelAsync();
            try
            {
                await pollerTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            scope.Dispose();
            cts.Dispose();
        }
    }
}
