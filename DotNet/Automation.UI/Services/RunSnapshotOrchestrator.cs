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

    private readonly IAutomationUiMetrics? _metrics;

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
        await StopAllPollersAsync();
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
                StartPoller(meta, CancellationToken.None);
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
        // Stop the existing poller before clearing snapshots. A finalizing poller is
        // still the last writer for the old report, so wait until that flush stops.
        var isMetricsRun = false;
        while (_activePollers.TryGetValue(runId, out var existingHandle))
        {
            if (existingHandle.TryDetach())
            {
                isMetricsRun = existingHandle.IsMetricsRun;
                TryRemoveExact(_activePollers, runId, existingHandle);
                await existingHandle.StopAsync();
                _logger.LogInformation("Stopped existing poller for run {RunId} before context switch", runId);
                break;
            }

            _logger.LogInformation("Waiting for the poller for run {RunId} to stop before changing its report", runId);
            await existingHandle.WhenReleased.WaitAsync(ct);
            if (_activePollers.TryGetValue(runId, out var stillThere) && ReferenceEquals(stillThere, existingHandle))
            {
                TryRemoveExact(_activePollers, runId, existingHandle);
                break;
            }
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

            if (registered.TryDetach())
            {
                TryRemoveExact(_activePollers, runId, registered);
                await registered.StopAsync();
            }
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
        StartPoller(meta, ct);

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
        }
    }

    private async Task CompleteRunCoreAsync(Guid runId)
    {
        // Stop the loop before the final flush. The loop and the final poll
        // write the same domains, and the final poll has to be the last writer.
        // This method takes no caller token. Cancelling here would skip that flush.
        // DrainAsync already cancels the polling loop.
        RunPollerHandle? activeHandle = null;
        var ownsHandle = false;
        if (_activePollers.TryGetValue(runId, out var candidate) && candidate.TryBeginFinalizing())
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

                if (_activePollers.TryGetValue(meta.RunId, out var existingHandle) && existingHandle.IsFinalizing)
                    continue;

                if (_activePollers.TryGetValue(meta.RunId, out existingHandle) && existingHandle.IsCompleted)
                {
                    if (existingHandle.TryDetach())
                    {
                        TryRemoveExact(_activePollers, meta.RunId, existingHandle);
                        await existingHandle.StopAsync();
                        _logger.LogWarning("Restarting completed poller task for still-active run {RunId}", meta.RunId);
                    }
                }

                // If run identifiers changed (e.g., regenerate switched to a new reportId),
                // force a poller restart so snapshots cannot oscillate between contexts.
                if (_activePollers.TryGetValue(meta.RunId, out existingHandle)
                    && (!string.Equals(existingHandle.FacilityId, meta.FacilityId, StringComparison.Ordinal)
                        || !string.Equals(existingHandle.ReportId, meta.ReportId, StringComparison.Ordinal)))
                {
                    if (existingHandle.TryDetach())
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

                if (!current.TryDetach())
                    continue;

                TryRemoveExact(_activePollers, runId, current);
                await current.StopAsync();
                _logger.LogInformation("Removed stale poller for run {RunId}", runId);
            }
            finally
            {
                gate.Release();
            }
        }
    }

    private SemaphoreSlim Gate(Guid runId)
        => _runGates.GetOrAdd(runId, static _ => new SemaphoreSlim(1, 1));

    internal static bool TryRemoveExact<T>(ConcurrentDictionary<Guid, T> pollers, Guid runId, T expected)
        where T : class
        => pollers.TryRemove(new KeyValuePair<Guid, T>(runId, expected));

    private void StartPoller(RunSnapshotMeta meta, CancellationToken ct)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // Build a scoped service provider for the poller's API clients
        var scope = _services.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<PipelineDataReader>();
        var poller = new StoreBackedServicePoller(_store, reader, meta, _logger, _metrics);

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

    private async Task StopAllPollersAsync()
    {
        // Completion holds this run's gate across the final poll, and that poll
        // still uses the handle's scoped clients. Wait for the gate, then detach
        // and remove this exact handle before stopping it.
        var stops = new List<Task>();
        foreach (var (runId, handle) in _activePollers.ToArray())
            stops.Add(StopPollerForShutdownAsync(runId, handle));

        await Task.WhenAll(stops);
    }

    private async Task StopPollerForShutdownAsync(Guid runId, RunPollerHandle handle)
    {
        var gate = Gate(runId);
        await gate.WaitAsync();
        try
        {
            if (!_activePollers.TryGetValue(runId, out var current) || !ReferenceEquals(current, handle))
                return;

            // A finalizing handle is only marked while completion holds this gate,
            // so a failed detach means another shutdown pass already claimed it.
            if (!current.TryDetach())
                return;

            TryRemoveExact(_activePollers, runId, current);
            await current.StopAsync();
            _logger.LogInformation("Stopped poller for run {RunId} during shutdown", runId);
        }
        finally
        {
            gate.Release();
        }
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

        private readonly PollerSlot _slot = new();

        public bool IsFinalizing => _slot.IsFinalizing;

        public bool TryBeginFinalizing() => _slot.TryBeginFinalizing();

        public bool TryDetach() => _slot.TryDetach();

        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _stopped;

        public Task WhenReleased => _released.Task;

        public Task FinalPollAsync() => poller.FinalPollAsync();

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
            {
                await _released.Task;
                return;
            }

            try
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
            finally
            {
                _released.TrySetResult();
            }
        }
    }
}

/// <summary>
/// One running poller can be finalized or detached, not both.
/// Finalizing keeps the handle for the last flush. Detaching lets reconcile stop it.
/// </summary>
internal sealed class PollerSlot
{
    private readonly object _gate = new();
    private bool _finalizing;
    private bool _detached;

    public bool IsFinalizing
    {
        get
        {
            lock (_gate)
                return _finalizing;
        }
    }

    public bool TryBeginFinalizing()
    {
        lock (_gate)
        {
            if (_detached || _finalizing)
                return false;

            _finalizing = true;
            return true;
        }
    }

    public bool TryDetach()
    {
        lock (_gate)
        {
            if (_finalizing || _detached)
                return false;

            _detached = true;
            return true;
        }
    }
}
