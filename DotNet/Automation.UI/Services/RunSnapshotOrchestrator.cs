using System.Collections.Concurrent;
using LantanaGroup.Link.Automation.Link.Helpers;

namespace Automation.UI.Services;

/// <summary>
/// Long-running background service that manages per-run data pollers.
/// Periodically checks for active runs and ensures each one has a poller.
/// When a run completes, its poller is stopped and removed.
///
/// This is the single place that does API polling � the UI controllers
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
    /// Updates a run's facility/report IDs and restarts the poller.
    /// Used when a regeneration produces a new report ID that we need to track.
    /// </summary>
    public async Task UpdateRunAsync(Guid runId, string facilityId, string reportId, CancellationToken ct = default)
    {
        // 1. Stop the existing poller FIRST so it cannot write stale domain data
        //    after we clear snapshots. StopAsync awaits the current poll cycle, so
        //    after this returns the old poller is guaranteed idle.
        if (_activePollers.TryRemove(runId, out var existingHandle))
        {
            await existingHandle.StopAsync();
            _logger.LogInformation("Stopped existing poller for run {RunId} before context switch", runId);
        }

        // 2. Now safe to update meta and clear stale domain snapshots � no writer
        //    can re-populate them with old-report data.
        await _store.UpdateRunMetaAsync(runId, facilityId, reportId, ct);

        // 3. Immediately start a new poller bound to the regenerated report so the
        //    UI doesn't have to wait up to 2s for the next reconciliation cycle.
        var meta = new RunSnapshotMeta
        {
            RunId = runId,
            FacilityId = facilityId,
            ReportId = reportId,
            StartedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };
        StartPoller(meta, ct);

        _logger.LogInformation("Updated run {RunId} to track new report {ReportId} and started new poller", runId, reportId);
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

        // Extract pipeline duration (report created ? submitted) from the persisted schedule.
        string? duration = null;
        try
        {
            var schedule = await _store.GetDomainAsync<PipelineDataReader.ReportScheduleInfo>(runId, "schedule");
            var createDate = schedule?.Data?.CreateDate;
            var submitReportDateTime = schedule?.Data?.SubmitReportDateTime;
            if (createDate.HasValue && submitReportDateTime.HasValue)
            {
                var span = submitReportDateTime.Value - createDate.Value;
                if (span.TotalSeconds >= 1)
                    duration = span.TotalHours >= 1
                        ? $"{(int)span.TotalHours}h {span.Minutes}m {span.Seconds}s"
                        : span.TotalMinutes >= 1
                            ? $"{(int)span.TotalMinutes}m {span.Seconds}s"
                            : $"{span.Seconds}s";
                else
                    duration = "< 1s";
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not extract pipeline duration for run {RunId}", runId);
        }

        await _store.CompleteRunAsync(runId, duration);

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

            // If run identifiers changed (e.g., regenerate switched to a new reportId),
            // force a poller restart so snapshots cannot oscillate between contexts.
            if (_activePollers.TryGetValue(meta.RunId, out existingHandle)
                && (!string.Equals(existingHandle.FacilityId, meta.FacilityId, StringComparison.Ordinal)
                    || !string.Equals(existingHandle.ReportId, meta.ReportId, StringComparison.Ordinal)))
            {
                if (_activePollers.TryRemove(meta.RunId, out var staleHandle))
                {
                    await staleHandle.StopAsync();
                    _logger.LogInformation(
                        "Restarting poller for run {RunId} due to context change (facility: {OldFacility}->{NewFacility}, report: {OldReport}->{NewReport})",
                        meta.RunId,
                        staleHandle.FacilityId,
                        meta.FacilityId,
                        staleHandle.ReportId,
                        meta.ReportId);
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
            _ = DisposeUnregisteredPollerAsync(task, cts, scope);
        }
    }

    private static async Task DisposeUnregisteredPollerAsync(Task task, CancellationTokenSource cts, IServiceScope scope)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // Expected after cancellation because another poller is already registered.
        }
        finally
        {
            scope.Dispose();
            cts.Dispose();
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
        public string FacilityId => poller.FacilityId;
        public string ReportId => poller.ReportId;
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
