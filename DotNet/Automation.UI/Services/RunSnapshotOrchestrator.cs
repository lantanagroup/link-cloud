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
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _runGates = new();
    private readonly ConcurrentDictionary<Guid, Task> _completions = new();
    private int _shuttingDown;
    private CancellationToken _stopToken;

    private readonly IAutomationUiMetrics? _metrics;

    /// <summary>
    /// Test seam. Runs after shutdown starts and before it blocks new pollers,
    /// so a report switch can still replace the handle shutdown has already seen.
    /// </summary>
    internal Func<Task>? AfterShutdownSnapshot;

    public RunSnapshotOrchestrator(
        ISnapshotStore store,
        IServiceProvider services,
        ILogger<RunSnapshotOrchestrator> logger,
        IAutomationUiMetrics? metrics = null)
    {
        _store = store;
        _services = services;
        _logger = logger;
        _metrics = metrics;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stopToken = stoppingToken;
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
                await Task.Delay(await NextOrchestrationDelayAsync(stoppingToken), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        // Shutdown: stop all pollers
        await StopAllPollersAsync(stoppingToken);
        _logger.LogInformation("RunSnapshotOrchestrator stopped");
    }

    /// <summary>
    /// Registers a new run so the orchestrator will start polling for it.
    /// Called by <see cref="AutomationRunManager"/> when facility + report are known.
    /// </summary>
    public async Task RegisterRunAsync(Guid runId, string facilityId, string reportId, bool isMetricsRun = false)
    {
        var meta = new RunSnapshotMeta
        {
            RunId = runId,
            FacilityId = facilityId,
            ReportId = reportId,
            StartedAt = DateTimeOffset.UtcNow,
            IsActive = true,
            IsMetricsRun = isMetricsRun
        };

        await _store.RegisterRunAsync(runId, meta);
        var gate = Gate(runId);
        await gate.WaitAsync();
        try
        {
            if (!_activePollers.ContainsKey(runId))
                await StartPollerAsync(meta, CancellationToken.None);
        }
        finally
        {
            gate.Release();
        }

        _logger.LogInformation("Registered run {RunId} for snapshot polling", runId);
    }

    /// <summary>
    /// Updates a run's facility/report IDs and restarts the poller.
    /// Used when a regeneration produces a new report ID that we need to track.
    /// </summary>
    public async Task UpdateRunAsync(Guid runId, string facilityId, string reportId, CancellationToken ct = default)
    {
        // Hold this run's gate across the whole switch. Reconcile reads the same
        // gate, so it cannot start a poller from the old report while snapshots
        // are cleared or before the new poller is registered.
        var gate = Gate(runId);
        await gate.WaitAsync(ct);
        try
        {
            await UpdateRunCoreAsync(runId, facilityId, reportId, ct);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task UpdateRunCoreAsync(Guid runId, string facilityId, string reportId, CancellationToken ct)
    {
        // Stop the existing poller before clearing snapshots. Completion holds
        // this same gate through the final poll, so this stop cannot dispose
        // a scope that flush is still using.
        var isMetricsRun = false;
        if (_activePollers.TryGetValue(runId, out var existingHandle))
        {
            isMetricsRun = existingHandle.IsMetricsRun;
            TryRemoveExact(_activePollers, runId, existingHandle);
            await existingHandle.StopAsync();
            _logger.LogInformation("Stopped existing poller for run {RunId} before context switch", runId);
        }

        // No writer for this run is still flushing. Clear the old report snapshots.
        await _store.UpdateRunMetaAsync(runId, facilityId, reportId, ct);

        // A replacement may have been registered while this update waited.
        if (_activePollers.TryGetValue(runId, out var registered))
        {
            if (string.Equals(registered.FacilityId, facilityId, StringComparison.Ordinal)
                && string.Equals(registered.ReportId, reportId, StringComparison.Ordinal))
            {
                _logger.LogInformation("Leaving the poller already registered for run {RunId}", runId);
                return;
            }

            TryRemoveExact(_activePollers, runId, registered);
            await registered.StopAsync();
        }

        if (_activePollers.ContainsKey(runId))
        {
            _logger.LogInformation("Leaving the poller already registered for run {RunId}", runId);
            return;
        }

        if (!isMetricsRun)
            isMetricsRun = (await _store.GetRunMetaAsync(runId, ct))?.IsMetricsRun ?? false;
        var meta = new RunSnapshotMeta
        {
            RunId = runId,
            FacilityId = facilityId,
            ReportId = reportId,
            StartedAt = DateTimeOffset.UtcNow,
            IsActive = true,
            IsMetricsRun = isMetricsRun
        };
        if (await StartPollerAsync(meta, ct))
            _logger.LogInformation("Updated run {RunId} to track new report {ReportId} and started new poller", runId, reportId);
    }

    /// <summary>
    /// Marks a run as complete so the orchestrator stops polling.
    /// A second caller waits for the completion already in flight.
    /// </summary>
    public Task CompleteRunAsync(Guid runId)
    {
        while (true)
        {
            if (_completions.TryGetValue(runId, out var existing))
                return existing;

            var work = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_completions.TryAdd(runId, work.Task))
                continue;

            _ = FinishCompletionAsync(runId, work);
            return work.Task;
        }
    }

    private async Task FinishCompletionAsync(Guid runId, TaskCompletionSource work)
    {
        // Same gate as a report switch. Completion waits until that switch
        // finishes, then finalizes whichever poller is registered.
        var gate = Gate(runId);
        await gate.WaitAsync();
        try
        {
            await CompleteRunCoreAsync(runId);
            work.TrySetResult();
        }
        catch (Exception ex)
        {
            work.TrySetException(ex);
        }
        finally
        {
            gate.Release();
            _completions.TryRemove(new KeyValuePair<Guid, Task>(runId, work.Task));
            PruneGate(runId);
        }
    }

    private async Task CompleteRunCoreAsync(Guid runId)
    {
        // Stop the loop before the final flush. The loop and the final poll
        // write the same domains, and the final poll has to be the last writer.
        // DrainAsync already cancels the polling loop. The host stop token
        // cancels the final poll so shutdown does not wait on it.
        RunPollerHandle? activeHandle = null;
        var ownsHandle = false;
        if (_activePollers.TryGetValue(runId, out var candidate))
        {
            activeHandle = candidate;
            ownsHandle = true;
        }

        if (activeHandle != null)
        {
            try
            {
                await activeHandle.DrainAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Stopping the poller before the final snapshot for run {RunId} failed", runId);
            }

            try
            {
                await activeHandle.FinalPollAsync(_stopToken);
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

        try
        {
            await _store.CompleteRunAsync(runId, duration);
        }
        finally
        {
            if (ownsHandle && activeHandle != null)
            {
                try
                {
                    TryRemoveExact(_activePollers, runId, activeHandle);
                    await activeHandle.StopAsync();
                    _logger.LogInformation("Stopped poller for completed run {RunId}", runId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Stopping the poller for completed run {RunId} failed", runId);
                }
            }
        }
    }

    private async Task ReconcileAsync(CancellationToken ct)
    {
        var activeRuns = await _store.GetActiveRunsAsync(ct);
        var activeRunIds = activeRuns.Select(r => r.RunId).ToHashSet();

        // Start pollers for runs that don't have one yet. The per-run gate is the
        // same one UpdateRunAsync holds, and the metadata is read again inside it.
        foreach (var listed in activeRuns)
        {
            var gate = Gate(listed.RunId);
            if (!await gate.WaitAsync(0, ct))
                continue;
            try
            {
                var meta = await _store.GetRunMetaAsync(listed.RunId, ct);
                if (meta == null || !meta.IsActive)
                    continue;

                if (_activePollers.TryGetValue(meta.RunId, out var existingHandle) && existingHandle.IsCompleted)
                {
                    TryRemoveExact(_activePollers, meta.RunId, existingHandle);
                    await existingHandle.StopAsync();
                    _logger.LogWarning("Restarting completed poller task for still-active run {RunId}", meta.RunId);
                }

                // If run identifiers changed (e.g., regenerate switched to a new reportId),
                // force a poller restart so snapshots cannot oscillate between contexts.
                if (_activePollers.TryGetValue(meta.RunId, out existingHandle)
                    && (!string.Equals(existingHandle.FacilityId, meta.FacilityId, StringComparison.Ordinal)
                        || !string.Equals(existingHandle.ReportId, meta.ReportId, StringComparison.Ordinal)))
                {
                    var oldFacilityId = existingHandle.FacilityId;
                    var oldReportId = existingHandle.ReportId;
                    TryRemoveExact(_activePollers, meta.RunId, existingHandle);
                    await existingHandle.StopAsync();
                    _logger.LogInformation(
                        "Restarting poller for run {RunId} due to context change (facility: {OldFacility}->{NewFacility}, report: {OldReport}->{NewReport})",
                        meta.RunId,
                        oldFacilityId,
                        meta.FacilityId,
                        oldReportId,
                        meta.ReportId);
                }

                if (!_activePollers.ContainsKey(meta.RunId))
                {
                    if (string.IsNullOrWhiteSpace(meta.FacilityId) || string.IsNullOrWhiteSpace(meta.ReportId))
                    {
                        _logger.LogDebug("Skipping poller start for run {RunId}: missing facility/report identifiers", meta.RunId);
                        continue;
                    }

                    await StartPollerAsync(meta, ct);
                }
            }
            finally
            {
                gate.Release();
            }
        }

        // Stop pollers for runs no longer active
        foreach (var (runId, handle) in _activePollers.ToArray())
        {
            if (activeRunIds.Contains(runId))
                continue;

            var gate = Gate(runId);
            if (!await gate.WaitAsync(0, ct))
                continue;
            try
            {
                if (!_activePollers.TryGetValue(runId, out var current) || !ReferenceEquals(current, handle))
                    continue;

                // The active list was captured before this gate. A run registered
                // after that read is still active and must keep its poller.
                var meta = await _store.GetRunMetaAsync(runId, ct);
                if (meta is { IsActive: true })
                    continue;

                TryRemoveExact(_activePollers, runId, current);
                await current.StopAsync();
                _logger.LogInformation("Removed stale poller for run {RunId}", runId);
            }
            finally
            {
                gate.Release();
                PruneGate(runId);
            }
        }
    }

    private void PruneGate(Guid runId)
    {
        if (_activePollers.ContainsKey(runId) || _completions.ContainsKey(runId))
            return;

        if (!_runGates.TryRemove(runId, out var gate))
            return;

        // A report switch may already be waiting on this gate. Leave it in place
        // then. Dispose only when nobody holds it and nobody is queued.
        if (!gate.Wait(0))
        {
            _runGates.TryAdd(runId, gate);
            return;
        }

        gate.Release();
        gate.Dispose();
    }

    private SemaphoreSlim Gate(Guid runId)
        => _runGates.GetOrAdd(runId, static _ => new SemaphoreSlim(1, 1));

    internal static bool TryRemoveExact<T>(ConcurrentDictionary<Guid, T> pollers, Guid runId, T expected)
        where T : class
        => pollers.TryRemove(new KeyValuePair<Guid, T>(runId, expected));

    private async Task<bool> StartPollerAsync(RunSnapshotMeta meta, CancellationToken ct)
    {
        if (Volatile.Read(ref _shuttingDown) != 0)
            return false;

        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // Build a scoped service provider for the poller's API clients
        var scope = _services.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<PipelineDataReader>();
        var poller = new StoreBackedServicePoller(_store, reader, meta, _logger, _metrics);

        var task = poller.RunAsync(cts.Token);
        var handle = new RunPollerHandle(cts, task, scope, poller);

        if (!_activePollers.TryAdd(meta.RunId, handle))
        {
            cts.Cancel();
            await DisposeUnregisteredPollerAsync(task, cts, scope);
            return false;
        }

        // Shutdown can begin after the check above. Stop this handle before
        // the caller releases the run gate, so the sweep cannot miss it.
        if (Volatile.Read(ref _shuttingDown) != 0)
        {
            TryRemoveExact(_activePollers, meta.RunId, handle);
            await handle.StopAsync();
            _logger.LogInformation("Stopped poller for run {RunId} because shutdown started", meta.RunId);
            return false;
        }

        _logger.LogInformation("Started poller for run {RunId} (facility={FacilityId})", meta.RunId, meta.FacilityId);
        return true;
    }

    private async Task DisposeUnregisteredPollerAsync(Task task, CancellationTokenSource cts, IServiceScope scope)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // Expected after cancellation because another poller is already registered.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while disposing unregistered poller task.");
        }
        finally
        {
            scope.Dispose();
            cts.Dispose();
        }
    }

    private async Task<TimeSpan> NextOrchestrationDelayAsync(CancellationToken ct)
    {
        try
        {
            var activeRuns = await _store.GetActiveRunsAsync(ct);
            var anyMetrics = activeRuns.Any(r => r.IsMetricsRun)
                || _activePollers.Values.Any(h => h.IsMetricsRun);
            return AutomationRunPollingPolicy.OrchestratorInterval(anyMetrics);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Falling back to lightweight orchestrator interval");
            return AutomationRunPollingPolicy.LightweightOrchestratorInterval;
        }
    }

    private async Task StopAllPollersAsync(CancellationToken stoppingToken)
    {
        // A report switch can still replace the handle during this hook.
        // The flag after it refuses any later registration, and the stop
        // below covers the handle that is current, including that replacement.
        if (AfterShutdownSnapshot != null)
            await AfterShutdownSnapshot();

        Volatile.Write(ref _shuttingDown, 1);

        var pending = _activePollers.ToArray();
        foreach (var (_, handle) in pending)
            handle.Cancel();

        if (stoppingToken.IsCancellationRequested)
            return;

        try
        {
            await Task.WhenAll(pending.Select(pair => StopPollerForShutdownAsync(pair.Key, pair.Value)))
                .WaitAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown wins. Do not wait out a final poll.
        }
    }

    private async Task StopPollerForShutdownAsync(Guid runId, RunPollerHandle handle)
    {
        if (!_activePollers.TryGetValue(runId, out var current) || !ReferenceEquals(current, handle))
            return;

        TryRemoveExact(_activePollers, runId, current);
        await current.StopAsync();
        _logger.LogInformation("Stopped poller for run {RunId} during shutdown", runId);
        PruneGate(runId);
    }

    private sealed class RunPollerHandle(
        CancellationTokenSource cts,
        Task pollerTask,
        IServiceScope scope,
        StoreBackedServicePoller poller)
    {
        public string FacilityId => poller.FacilityId;
        public string ReportId => poller.ReportId;
        public bool IsMetricsRun => poller.IsMetricsRun;
        public bool IsCompleted => pollerTask.IsCompleted;

        private int _stopped;

        public void Cancel() => cts.Cancel();

        public Task FinalPollAsync(CancellationToken cancellationToken) => poller.FinalPollAsync(cancellationToken);

        public async Task DrainAsync()
        {
            await cts.CancelAsync();
            try
            {
                await pollerTask;
            }
            catch (OperationCanceledException)
            {
                // The loop stops here. StopAsync still disposes the scope after the final poll.
            }
        }

        public async Task StopAsync()
        {
            if (Interlocked.Exchange(ref _stopped, 1) == 1)
                return;

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
