using Automation.UI.Models;
using Automation.UI.Services.Persistence;
using LantanaGroup.Link.Automation.Link.Models;
using LantanaGroup.Link.Shared.Application.Services.Security;

namespace Link.UI.Services.ReportBatches;

/// <summary>
/// Starts report runs up to a batch's cap and keeps the rest queued.
/// One run's failure does not stop the others. In-flight runs are not resumed
/// after a restart: startup recovery has already cancelled or deleted them.
/// </summary>
public sealed class ReportBatchDispatcher : IHostedService
{
    public const string InterruptedByRestart = "Interrupted by process restart.";
    public const string CancelledBeforeStart = "Cancelled before start.";

    private readonly IReportBatchStore _batches;
    private readonly IReportBatchSettingsStore _settings;
    private readonly IScenarioStore _scenarios;
    private readonly IReportBatchEhrLookup _ehr;
    private readonly IReportBatchRunGateway _runs;
    private readonly TimeProvider _clock;
    private readonly ILogger<ReportBatchDispatcher> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SemaphoreSlim _wake = new(0, 1);
    private readonly CancellationTokenSource _stop = new();
    private Task? _loop;
    private int _signaled;

    public ReportBatchDispatcher(
        IReportBatchStore batches,
        IReportBatchSettingsStore settings,
        IScenarioStore scenarios,
        IReportBatchEhrLookup ehr,
        IReportBatchRunGateway runs,
        TimeProvider clock,
        ILogger<ReportBatchDispatcher> logger)
    {
        _batches = batches;
        _settings = settings;
        _scenarios = scenarios;
        _ehr = ehr;
        _runs = runs;
        _clock = clock;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _batches.EnsureIndexesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Report batch indexes could not be ensured.");
        }

        try
        {
            await RecoverAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Report batch recovery failed. New batches can still start.");
        }

        _loop = Task.Run(() => LoopAsync(_stop.Token), CancellationToken.None);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stop.CancelAsync();
        if (_loop is null)
            return;

        try
        {
            await _loop.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The host stopped waiting. The loop ends on its own token.
        }
    }

    public async Task<ReportBatchDocument> StartBatchAsync(
        string? ehrSystem,
        IReadOnlyList<Guid>? scenarioIds,
        int? maxConcurrency,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var batch = await CreateBatchAsync(ehrSystem, scenarioIds, maxConcurrency, cancellationToken);
            await _batches.InsertAsync(batch, cancellationToken);
            await DispatchOneAsync(batch, cancellationToken);
            _logger.LogInformation(
                "Started report batch {BatchId} on {EhrSystem} with {RunCount} runs at cap {Cap}.",
                batch.BatchId,
                batch.EhrSystem.Sanitize(),
                batch.Items.Count,
                batch.MaxConcurrency);
            return batch;
        }
        finally
        {
            _gate.Release();
            Wake();
        }
    }

    public async Task<ReportBatchDocument?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var batch = await _batches.GetAsync(batchId, cancellationToken);
            if (batch is null || !ReportBatchStatus.IsOpen(batch.Status))
                return batch;

            await DispatchOneAsync(batch, cancellationToken);
            return batch;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ReportBatchDocument?> CancelBatchAsync(Guid batchId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var batch = await _batches.GetAsync(batchId, cancellationToken);
            if (batch is null)
                return null;

            if (!ReportBatchStatus.IsOpen(batch.Status))
                return batch;

            batch.CancelRequested = true;
            foreach (var item in batch.Items)
            {
                if (item.Status != ReportBatchItemStatus.Queued)
                    continue;

                item.Status = ReportBatchItemStatus.Cancelled;
                item.Error = CancelledBeforeStart;
            }

            await SaveAsync(batch, cancellationToken);
            await DispatchOneAsync(batch, cancellationToken);
            return batch;
        }
        finally
        {
            _gate.Release();
            Wake();
        }
    }

    public async Task RecoverAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var batches = await _batches.ListActiveAsync(cancellationToken);
            foreach (var batch in batches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await InterruptInFlightAsync(batch, cancellationToken);
                await SaveAsync(batch, cancellationToken);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task PumpAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var batches = await _batches.ListActiveAsync(cancellationToken);
            foreach (var batch in batches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await DispatchOneAsync(batch, cancellationToken);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ReportBatchDocument> CreateBatchAsync(
        string? ehrSystem,
        IReadOnlyList<Guid>? scenarioIds,
        int? maxConcurrency,
        CancellationToken cancellationToken)
    {
        var ehr = _ehr.CanonicalName(ehrSystem.Sanitize());
        if (ehr is null)
            throw new InvalidOperationException("ehrSystem is required and must be a configured EHR system.");

        if (scenarioIds is null || scenarioIds.Count == 0)
            throw new InvalidOperationException("At least one scenario is required.");

        if (scenarioIds.Count > ReportBatchConcurrencyPolicy.MaxScenariosPerBatch)
        {
            throw new InvalidOperationException(
                $"A batch can include at most {ReportBatchConcurrencyPolicy.MaxScenariosPerBatch} scenarios.");
        }

        var savedCap = await _settings.GetMaxConcurrencyAsync(cancellationToken);
        var cap = ReportBatchConcurrencyPolicy.ResolveBatchCap(maxConcurrency, savedCap);
        var now = _clock.GetUtcNow();
        var items = new List<ReportBatchItemDocument>(scenarioIds.Count);
        for (var index = 0; index < scenarioIds.Count; index++)
        {
            var scenarioId = scenarioIds[index];
            if (scenarioId == Guid.Empty)
                throw new InvalidOperationException("scenarioId must be a UUID.");

            var scenario = await _scenarios.GetByIdAsync(scenarioId, cancellationToken);
            if (scenario is null)
                throw new InvalidOperationException($"Scenario {scenarioId} was not found.");

            items.Add(new ReportBatchItemDocument
            {
                Ordinal = index,
                ScenarioId = scenario.Id,
                ScenarioName = scenario.Name,
                IsMetricsRun = scenario.IsMetricsRun,
                Status = ReportBatchItemStatus.Queued
            });
        }

        return new ReportBatchDocument
        {
            BatchId = Guid.NewGuid(),
            EhrSystem = ehr,
            MaxConcurrency = cap,
            Status = ReportBatchStatus.Queued,
            Items = items,
            CreatedAt = now
        };
    }

    private async Task DispatchOneAsync(ReportBatchDocument batch, CancellationToken cancellationToken)
    {
        if (batch.CancelRequested)
            await CancelRunningAsync(batch, cancellationToken);
        else
            await ReconcileAndFillAsync(batch, cancellationToken);

        await SaveAsync(batch, cancellationToken);
    }

    private async Task ReconcileAndFillAsync(ReportBatchDocument batch, CancellationToken cancellationToken)
    {
        foreach (var item in batch.Items.Where(row => row.Status == ReportBatchItemStatus.Running))
            await ApplyObservedRunAsync(item, missingIsFailure: true, cancellationToken);

        while (RunningCount(batch) < batch.MaxConcurrency)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var next = batch.Items
                .Where(row => row.Status == ReportBatchItemStatus.Queued)
                .OrderBy(row => row.Ordinal)
                .FirstOrDefault(row => !BlockedByOwnMetricsRun(batch, row));
            if (next is null)
                break;

            if (!await TryStartAsync(batch, next, cancellationToken))
                continue;

            try
            {
                await SaveAsync(batch, cancellationToken);
            }
            catch (Exception ex)
            {
                // The run is already going, but the store still has this item queued
                // with no run id. Cancel it on a token the request cannot cancel,
                // and let the next pump start one run.
                await AbandonUnsavedStartAsync(next, ex);
                throw;
            }
        }
    }

    private async Task<bool> TryStartAsync(
        ReportBatchDocument batch,
        ReportBatchItemDocument item,
        CancellationToken cancellationToken)
    {
        var scenario = await _scenarios.GetByIdAsync(item.ScenarioId, cancellationToken);
        if (scenario is null)
        {
            item.Status = ReportBatchItemStatus.Failed;
            item.Error = $"Scenario {item.ScenarioId} was not found.";
            return false;
        }

        var request = StartScenarioRequest.FromScenario(scenario);
        request.EhrSystem = batch.EhrSystem;
        try
        {
            item.RunId = await _runs.StartAsync(request, cancellationToken);
            item.Status = ReportBatchItemStatus.Running;
            item.Error = null;
            item.IsMetricsRun = scenario.IsMetricsRun;
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            item.Status = ReportBatchItemStatus.Failed;
            item.Error = ex.Message;
            _logger.LogWarning(
                ex,
                "Report batch {BatchId} did not start scenario {ScenarioId}. The other runs continue.",
                batch.BatchId,
                item.ScenarioId);
            return false;
        }
    }

    private async Task AbandonUnsavedStartAsync(ReportBatchItemDocument item, Exception saveError)
    {
        var runId = item.RunId;
        item.Status = ReportBatchItemStatus.Queued;
        item.RunId = null;
        item.Error = null;
        if (runId is not Guid id)
            return;

        try
        {
            await _runs.CancelRunAsync(id, CancellationToken.None);
            _logger.LogWarning(
                saveError,
                "Batch save failed after starting run {RunId}. The run was cancelled and the item stays queued.",
                id);
        }
        catch (Exception cancelEx)
        {
            _logger.LogError(
                cancelEx,
                "Batch save failed after starting run {RunId}, and cancelling that run also failed.",
                id);
        }
    }

    private async Task CancelRunningAsync(ReportBatchDocument batch, CancellationToken cancellationToken)
    {
        foreach (var item in batch.Items.Where(row => row.Status == ReportBatchItemStatus.Running))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.RunId is Guid runId)
            {
                try
                {
                    await _runs.CancelRunAsync(runId, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    item.Error = ex.Message;
                    _logger.LogWarning(
                        ex,
                        "Cancel failed for run {RunId} in batch {BatchId}. The other runs still cancel.",
                        runId,
                        batch.BatchId);
                }
            }

            await ApplyObservedRunAsync(item, missingIsFailure: false, cancellationToken);
        }
    }

    /// <summary>
    /// A run that was still going when the process stopped is not started again.
    /// The run recovery service has already cancelled or deleted it.
    /// </summary>
    private async Task InterruptInFlightAsync(ReportBatchDocument batch, CancellationToken cancellationToken)
    {
        foreach (var item in batch.Items)
        {
            if (item.Status != ReportBatchItemStatus.Running && item.RunId is null)
                continue;

            if (ReportBatchItemStatus.IsTerminal(item.Status))
                continue;

            AutomationRunSummary? summary = null;
            if (item.RunId is Guid runId)
                summary = await _runs.GetRunAsync(runId, cancellationToken);

            if (summary is { } finished && finished.Status.IsTerminal())
            {
                ApplyTerminal(item, finished);
                continue;
            }

            if (summary is not null && item.RunId is Guid runningId)
            {
                try
                {
                    await _runs.CancelRunAsync(runningId, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Restart recovery could not cancel run {RunId}.", runningId);
                }
            }

            item.Status = ReportBatchItemStatus.Cancelled;
            item.Error = InterruptedByRestart;
        }
    }

    private async Task ApplyObservedRunAsync(
        ReportBatchItemDocument item,
        bool missingIsFailure,
        CancellationToken cancellationToken)
    {
        if (item.RunId is not Guid runId)
        {
            item.Status = ReportBatchItemStatus.Failed;
            item.Error = "Running item has no run id.";
            return;
        }

        var summary = await _runs.GetRunAsync(runId, cancellationToken);
        if (summary is null)
        {
            item.Status = missingIsFailure ? ReportBatchItemStatus.Failed : ReportBatchItemStatus.Cancelled;
            item.Error ??= "Run was not found.";
            return;
        }

        if (!summary.Status.IsTerminal())
            return;

        ApplyTerminal(item, summary);
    }

    private static void ApplyTerminal(ReportBatchItemDocument item, AutomationRunSummary summary)
    {
        item.Status = summary.Status switch
        {
            AutomationRunStatus.Succeeded => ReportBatchItemStatus.Succeeded,
            AutomationRunStatus.Failed => ReportBatchItemStatus.Failed,
            AutomationRunStatus.Cancelled => ReportBatchItemStatus.Cancelled,
            _ => item.Status
        };
        item.Error = summary.Error;
    }

    private static bool BlockedByOwnMetricsRun(ReportBatchDocument batch, ReportBatchItemDocument item)
    {
        if (!item.IsMetricsRun)
            return false;

        return batch.Items.Any(other =>
            other.Ordinal != item.Ordinal
            && other.IsMetricsRun
            && other.Status == ReportBatchItemStatus.Running);
    }

    private async Task SaveAsync(ReportBatchDocument batch, CancellationToken cancellationToken)
    {
        Rollup(batch, _clock.GetUtcNow());
        await _batches.ReplaceAsync(batch, cancellationToken);
    }

    private static void Rollup(ReportBatchDocument batch, DateTimeOffset now)
    {
        var anyOpen = batch.Items.Any(item => !ReportBatchItemStatus.IsTerminal(item.Status));
        if (batch.Items.Any(item => item.Status != ReportBatchItemStatus.Queued))
            batch.StartedAt ??= now;

        if (anyOpen)
        {
            batch.Status = batch.StartedAt is null ? ReportBatchStatus.Queued : ReportBatchStatus.Running;
            batch.FinishedAt = null;
            return;
        }

        batch.StartedAt ??= now;
        batch.FinishedAt ??= now;
        batch.Status = batch.CancelRequested ? ReportBatchStatus.Cancelled : ReportBatchStatus.Completed;
    }

    private static int RunningCount(ReportBatchDocument batch)
        => batch.Items.Count(item => item.Status == ReportBatchItemStatus.Running);

    private async Task LoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await PumpAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Report batch dispatch failed.");
            }

            try
            {
                await _wake.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
                Interlocked.Exchange(ref _signaled, 0);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private void Wake()
    {
        if (Interlocked.Exchange(ref _signaled, 1) != 0)
            return;

        try
        {
            _wake.Release();
        }
        catch (SemaphoreFullException)
        {
            Interlocked.Exchange(ref _signaled, 1);
        }
    }
}
