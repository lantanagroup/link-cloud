using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Automation.Link.Models;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Interfaces;
using Automation.UI.Services.Persistence;
using Microsoft.AspNetCore.SignalR;

namespace Automation.UI.Services;

/// <summary>
/// Two-phase leftover cleanup with off-hours heavy work:
/// cheap quiesce can run during the day; facility teardown is daily at 10:00 UTC;
/// scenario-run history purge is weekly on Sunday at 10:00 UTC.
/// </summary>
public sealed class LeftoverRunCleanupService(
    IServiceScopeFactory scopeFactory,
    ISnapshotStore snapshotStore,
    TimeProvider time,
    ICleanupSettingsStore settingsStore,
    IPipelineAbortRegistry abortRegistry,
    IHubContext<CleanupHub> cleanupHub,
    ILogger<LeftoverRunCleanupService> logger) : BackgroundService, ILeftoverRunCleanup
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset? _lastQuiesceAt;
    private CancellationToken _stopping;

    public DateTimeOffset? LastQuiesceAt => _lastQuiesceAt;
    public LeftoverCleanupResult? LastQuiesceResult { get; private set; }
    public LeftoverCleanupResult? LastTeardownResult { get; private set; }
    public LeftoverCleanupResult? LastHistoryPurgeResult { get; private set; }
    public CleanupActivity CurrentActivity { get; private set; } = CleanupActivity.Idle;
    public bool IsRunning { get; private set; }

    public async Task QuiesceFacilityAsync(
        string? facilityId,
        string? reportId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            return;

        var settings = await settingsStore.GetEffectiveAsync(cancellationToken);
        using var scope = scopeFactory.CreateScope();
        var dataAcqClient = scope.ServiceProvider.GetRequiredService<IDataAcquisitionServiceClient>();
        var censusClient = scope.ServiceProvider.GetRequiredService<ICensusServiceClient>();
        var reportClient = scope.ServiceProvider.GetRequiredService<IReportServiceClient>();
        var output = new LoggerAutomationOutput(logger, facilityId);

        await RunCleanupHelper.AbortAndQuiesceFacilityAsync(
            abortRegistry,
            dataAcqClient,
            censusClient,
            reportClient,
            output,
            facilityId,
            reportId,
            settings.AbortTtl,
            cancellationToken);
    }

    public void StartQuiesceInBackground()
        => Observe(RunQuiesceAsync(_stopping, trigger: "manual"));

    public void StartTeardownInBackground()
        => Observe(RunTeardownAsync(_stopping, trigger: "manual"));

    public void StartHistoryPurgeInBackground()
        => Observe(RunHistoryPurgeAsync(_stopping, trigger: "manual"));

    public void StartCustomRangeInBackground(
        DateTimeOffset fromInclusiveUtc,
        DateTimeOffset toExclusiveUtc,
        bool teardownFacilities,
        bool purgeHistory)
        => Observe(RunCustomRangeAsync(fromInclusiveUtc, toExclusiveUtc, teardownFacilities, purgeHistory, _stopping));

    public Task<LeftoverCleanupResult> RunQuiesceNowAsync(CancellationToken cancellationToken = default)
        => RunQuiesceAsync(cancellationToken, trigger: "manual");

    public Task<LeftoverCleanupResult> RunTeardownNowAsync(CancellationToken cancellationToken = default)
        => RunTeardownAsync(cancellationToken, trigger: "manual");

    public Task<LeftoverCleanupResult> RunHistoryPurgeNowAsync(CancellationToken cancellationToken = default)
        => RunHistoryPurgeAsync(cancellationToken, trigger: "manual");

    public Task<LeftoverCleanupResult> RunCustomRangeAsync(
        DateTimeOffset fromInclusiveUtc,
        DateTimeOffset toExclusiveUtc,
        bool teardownFacilities,
        bool purgeHistory,
        CancellationToken cancellationToken = default)
        => RunScopedAsync(
            "custom-range",
            "Custom range cleanup",
            "manual",
            (facilities, runs, now, settings) =>
            {
                var ranged = RunCleanupHelper.SelectRunsFinishedInRange(runs, fromInclusiveUtc, toExclusiveUtc);
                var facilityIds = teardownFacilities
                    ? RunCleanupHelper.SelectAutomationFacilitiesForRuns(facilities, ranged)
                    : [];
                var history = purgeHistory ? ranged : [];
                return (facilityIds, history);
            },
            cancellationToken,
            teardownFacilities: teardownFacilities,
            purgeHistory: purgeHistory);

    /// <summary>Kept for callers that want a one-shot leftover pass (quiesce then teardown).</summary>
    public async Task<LeftoverCleanupResult> RunOnceAsync(
        CancellationToken cancellationToken = default,
        int? maxFacilitiesOverride = null)
    {
        var quiesced = await RunQuiesceAsync(cancellationToken, maxFacilitiesOverride);
        var tornDown = await RunTeardownAsync(cancellationToken, maxFacilitiesOverride);
        return Combine(quiesced, tornDown);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stopping = stoppingToken;
        var startup = await settingsStore.GetEffectiveAsync(stoppingToken);
        if (!startup.Enabled)
        {
            logger.LogInformation("Leftover Automation facility cleanup is disabled.");
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var settings = await settingsStore.GetEffectiveAsync(stoppingToken);
                var now = time.GetUtcNow();

                if (settings.Enabled && settings.QuiesceEnabled && IsQuiesceDue(now, settings))
                {
                    var result = await RunQuiesceAsync(stoppingToken, trigger: "scheduled");
                    _lastQuiesceAt = now;
                    LastQuiesceResult = result;
                }

                if (settings.Enabled && settings.DailyTeardownEnabled
                    && CleanupSchedule.IsDueDaily(now, settings.DailyTeardownTimeUtc, settings.CatchUpWindow, settings.LastDailyTeardownAt))
                {
                    var result = await RunTeardownAsync(stoppingToken, trigger: "scheduled");
                    LastTeardownResult = result;
                    await settingsStore.RecordDailyTeardownAsync(now, FormatResult("Daily teardown", result), stoppingToken);
                }

                if (settings.Enabled && settings.WeeklyHistoryPurgeEnabled
                    && CleanupSchedule.IsDueWeekly(
                        now,
                        settings.WeeklyHistoryPurgeDay,
                        settings.WeeklyHistoryPurgeTimeUtc,
                        settings.CatchUpWindow,
                        settings.LastWeeklyPurgeAt))
                {
                    var result = await RunHistoryPurgeAsync(stoppingToken, trigger: "scheduled");
                    LastHistoryPurgeResult = result;
                    await settingsStore.RecordWeeklyPurgeAsync(now, FormatResult("Weekly history purge", result), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Leftover Automation facility sweep failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private bool IsQuiesceDue(DateTimeOffset now, LeftoverRunCleanupSettings settings)
        => _lastQuiesceAt is not DateTimeOffset last || now - last >= settings.QuiesceInterval;

    private Task<LeftoverCleanupResult> RunQuiesceAsync(
        CancellationToken cancellationToken,
        int? maxFacilitiesOverride = null,
        string trigger = "manual")
        => RunScopedAsync(
            "quiesce",
            "Quiesce leftover hot work",
            trigger,
            (facilities, runs, now, settings) =>
            {
                var ids = RunCleanupHelper.SelectQuiesceAutomationFacilities(
                    facilities, runs, now, settings.QuiesceGrace);
                return (ids, Array.Empty<AutomationRunSummary>());
            },
            cancellationToken,
            maxFacilitiesOverride,
            teardownFacilities: false,
            purgeHistory: false);

    private Task<LeftoverCleanupResult> RunTeardownAsync(
        CancellationToken cancellationToken,
        int? maxFacilitiesOverride = null,
        string trigger = "manual")
        => RunScopedAsync(
            "teardown",
            "Off-hours leftover teardown",
            trigger,
            (facilities, runs, now, settings) =>
            {
                var leftover = RunCleanupHelper.SelectTeardownAutomationFacilities(
                    facilities, runs, now, settings.TeardownRetention);
                var stale = RunCleanupHelper.SelectStaleActiveAutomationFacilities(
                    facilities, runs, now, settings.TeardownRetention);
                return (leftover.Concat(stale).Distinct(StringComparer.OrdinalIgnoreCase).ToList(), Array.Empty<AutomationRunSummary>());
            },
            cancellationToken,
            maxFacilitiesOverride,
            teardownFacilities: true,
            purgeHistory: false);

    private Task<LeftoverCleanupResult> RunHistoryPurgeAsync(
        CancellationToken cancellationToken,
        int? maxFacilitiesOverride = null,
        string trigger = "manual")
        => RunScopedAsync(
            "history-purge",
            "Weekly history purge",
            trigger,
            (facilities, runs, now, settings) =>
            {
                var leftover = RunCleanupHelper.SelectTeardownAutomationFacilities(
                    facilities, runs, now, settings.TeardownRetention);
                var stale = RunCleanupHelper.SelectStaleActiveAutomationFacilities(
                    facilities, runs, now, settings.TeardownRetention);
                var history = RunCleanupHelper.SelectHistoryPurgeRuns(runs, now, settings.TeardownRetention);
                return (
                    leftover.Concat(stale).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    history);
            },
            cancellationToken,
            maxFacilitiesOverride,
            teardownFacilities: true,
            purgeHistory: true);

    private async Task<LeftoverCleanupResult> RunScopedAsync(
        string mode,
        string label,
        string trigger,
        Func<
            IReadOnlyDictionary<string, string>,
            IReadOnlyList<AutomationRunSummary>,
            DateTimeOffset,
            LeftoverRunCleanupSettings,
            (IReadOnlyList<string> FacilityIds, IReadOnlyList<AutomationRunSummary> HistoryRuns)> select,
        CancellationToken cancellationToken,
        int? maxFacilitiesOverride = null,
        bool teardownFacilities = true,
        bool purgeHistory = false)
    {
        if (!await _gate.WaitAsync(TimeSpan.Zero, cancellationToken))
            throw new InvalidOperationException("A cleanup pass is already running.");

        IsRunning = true;
        try
        {
            var settings = await settingsStore.GetEffectiveAsync(cancellationToken);
            using var scope = scopeFactory.CreateScope();
            var facilityClient = scope.ServiceProvider.GetRequiredService<IFacilityServiceClient>();
            var normalizationClient = scope.ServiceProvider.GetRequiredService<INormalizationServiceClient>();
            var dataAcqClient = scope.ServiceProvider.GetRequiredService<IDataAcquisitionServiceClient>();
            var queryDispatchClient = scope.ServiceProvider.GetRequiredService<IQueryDispatchServiceClient>();
            var censusClient = scope.ServiceProvider.GetRequiredService<ICensusServiceClient>();
            var reportClient = scope.ServiceProvider.GetRequiredService<IReportServiceClient>();

            await PublishAsync(new CleanupActivity
            {
                Mode = mode,
                Label = label,
                Status = "running",
                Trigger = trigger,
                Message = "Selecting leftover work…",
                At = time.GetUtcNow()
            }, cancellationToken);

            var facilitiesResponse = await facilityClient.GetFacilityListAsync(cancellationToken: cancellationToken);
            var facilities = facilitiesResponse.Body ?? new Dictionary<string, string>();
            var runs = await snapshotStore.GetAllRunSummariesAsync(since: null, ct: cancellationToken);
            var now = time.GetUtcNow();
            var (facilityIds, historyRuns) = select(facilities, runs, now, settings);

            var limit = Math.Max(1, maxFacilitiesOverride ?? settings.MaxFacilitiesPerPass);
            var facilityWork = facilityIds.Take(limit).ToList();
            var historyWork = purgeHistory
                ? historyRuns.Take(Math.Max(limit, 200)).ToList()
                : [];
            var total = facilityWork.Count + historyWork.Count;
            var quiesced = new List<string>();
            var tornDown = new List<string>();
            var purged = new List<Guid>();
            var failedFacilities = new List<string>();
            var failedRuns = new List<Guid>();
            var processed = 0;

            await PublishProgressAsync(
                mode, label, trigger, total, processed, quiesced, tornDown, purged, failedFacilities, failedRuns,
                total == 0 ? "Nothing matched." : $"Working through {total} item{(total == 1 ? "" : "s")}…",
                currentItem: null, cancellationToken);

            foreach (var facilityId in facilityWork)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await PublishProgressAsync(
                    mode, label, trigger, total, processed, quiesced, tornDown, purged, failedFacilities, failedRuns,
                    teardownFacilities ? "Tearing down leftover facility" : "Quiescing leftover facility",
                    facilityId, cancellationToken);
                var output = new LoggerAutomationOutput(logger, facilityId);
                try
                {
                    if (teardownFacilities)
                    {
                        await RunCleanupHelper.CleanupLeftoverFacilityAsync(
                            facilityClient,
                            normalizationClient,
                            dataAcqClient,
                            queryDispatchClient,
                            censusClient,
                            reportClient,
                            abortRegistry,
                            output,
                            facilityId,
                            settings.AbortTtl,
                            cancellationToken);
                        tornDown.Add(facilityId);
                    }
                    else
                    {
                        await RunCleanupHelper.AbortAndQuiesceFacilityAsync(
                            abortRegistry,
                            dataAcqClient,
                            censusClient,
                            reportClient,
                            output,
                            facilityId,
                            reportId: null,
                            settings.AbortTtl,
                            cancellationToken);
                        quiesced.Add(facilityId);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Leftover facility {Mode} failed for {FacilityId}.", mode, facilityId);
                    failedFacilities.Add(facilityId);
                }

                processed++;
            }

            foreach (var run in historyWork)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await PublishProgressAsync(
                    mode, label, trigger, total, processed, quiesced, tornDown, purged, failedFacilities, failedRuns,
                    "Purging run history",
                    run.RunId.ToString(), cancellationToken);
                try
                {
                    await snapshotStore.DeleteRunAsync(run.RunId, cancellationToken);
                    purged.Add(run.RunId);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "History purge failed for run {RunId}.", run.RunId);
                    failedRuns.Add(run.RunId);
                }

                processed++;
            }

            var result = new LeftoverCleanupResult(
                facilityIds.Count,
                quiesced,
                facilityIds.Count,
                tornDown,
                historyRuns.Count,
                purged,
                failedFacilities,
                failedRuns);

            if (quiesced.Count > 0 || tornDown.Count > 0 || purged.Count > 0 || facilityIds.Count > 0 || historyRuns.Count > 0)
            {
                logger.LogInformation(
                    "Leftover Automation {Mode} finished. facilities={FacilityCandidates}, quiesced={Quiesced}, tornDown={TornDown}, history={HistoryCandidates}, purged={Purged}, failedFacilities={FailedFacilities}, failedRuns={FailedRuns}",
                    mode, facilityIds.Count, quiesced.Count, tornDown.Count, historyRuns.Count, purged.Count, failedFacilities.Count, failedRuns.Count);
            }

            await PublishAsync(new CleanupActivity
            {
                Mode = mode,
                Label = label,
                Status = failedFacilities.Count > 0 || failedRuns.Count > 0 ? "failed" : "completed",
                Trigger = trigger,
                Total = total,
                Processed = processed,
                Quiesced = quiesced.Count,
                TornDown = tornDown.Count,
                Purged = purged.Count,
                Failed = failedFacilities.Count + failedRuns.Count,
                Message = FormatActivityResult(label, result),
                At = time.GetUtcNow()
            }, cancellationToken);

            return result;
        }
        catch (OperationCanceledException)
        {
            await PublishAsync(CurrentActivity with
            {
                Status = "failed",
                Message = $"{label} cancelled.",
                At = time.GetUtcNow()
            }, CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            await PublishAsync(new CleanupActivity
            {
                Mode = mode,
                Label = label,
                Status = "failed",
                Trigger = trigger,
                Message = $"{label} failed: {ex.Message}",
                At = time.GetUtcNow()
            }, CancellationToken.None);
            throw;
        }
        finally
        {
            IsRunning = false;
            _gate.Release();
        }
    }

    private void Observe(Task<LeftoverCleanupResult> task)
        => _ = ObserveAsync(task);

    private async Task ObserveAsync(Task<LeftoverCleanupResult> task)
    {
        try
        {
            await task;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already running", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Skipped a cleanup start because a pass is already running.");
        }
        catch (OperationCanceledException)
        {
            // Host is stopping, or the pass was cancelled. Status already published.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Background leftover cleanup pass failed.");
        }
    }

    private Task PublishProgressAsync(
        string mode,
        string label,
        string trigger,
        int total,
        int processed,
        List<string> quiesced,
        List<string> tornDown,
        List<Guid> purged,
        List<string> failedFacilities,
        List<Guid> failedRuns,
        string message,
        string? currentItem,
        CancellationToken cancellationToken)
        => PublishAsync(new CleanupActivity
        {
            Mode = mode,
            Label = label,
            Status = "running",
            Trigger = trigger,
            Total = total,
            Processed = processed,
            Quiesced = quiesced.Count,
            TornDown = tornDown.Count,
            Purged = purged.Count,
            Failed = failedFacilities.Count + failedRuns.Count,
            CurrentItem = currentItem,
            Message = message,
            At = time.GetUtcNow()
        }, cancellationToken);

    private async Task PublishAsync(CleanupActivity activity, CancellationToken cancellationToken)
    {
        CurrentActivity = activity;
        try
        {
            await cleanupHub.Clients.Group(CleanupHub.Group).SendAsync("cleanupUpdate", activity, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not publish leftover cleanup activity to the Cleanup hub.");
        }
    }

    private static string FormatActivityResult(string label, LeftoverCleanupResult result)
    {
        if (result.QuiesceCandidateCount == 0 && result.TeardownCandidateCount == 0 && result.HistoryPurgeCandidateCount == 0)
            return $"{label}: nothing matched.";

        var message = $"{label}: stopped {result.QuiescedFacilityIds.Count} of {result.QuiesceCandidateCount} leftover facilit{(result.QuiesceCandidateCount == 1 ? "y" : "ies")}, torn down {result.TornDownFacilityIds.Count}, purged {result.PurgedRunIds.Count} run record{(result.PurgedRunIds.Count == 1 ? "" : "s")}.";
        if (result.FailedFacilityIds.Count > 0)
            message += $" Failed facilities: {string.Join(", ", result.FailedFacilityIds)}.";
        if (result.FailedRunIds.Count > 0)
            message += $" Failed runs: {string.Join(", ", result.FailedRunIds)}.";
        return message;
    }

    private static LeftoverCleanupResult Combine(LeftoverCleanupResult first, LeftoverCleanupResult second)
        => new(
            first.QuiesceCandidateCount + second.QuiesceCandidateCount,
            first.QuiescedFacilityIds.Concat(second.QuiescedFacilityIds).ToList(),
            first.TeardownCandidateCount + second.TeardownCandidateCount,
            first.TornDownFacilityIds.Concat(second.TornDownFacilityIds).ToList(),
            first.HistoryPurgeCandidateCount + second.HistoryPurgeCandidateCount,
            first.PurgedRunIds.Concat(second.PurgedRunIds).ToList(),
            first.FailedFacilityIds.Concat(second.FailedFacilityIds).ToList(),
            first.FailedRunIds.Concat(second.FailedRunIds).ToList());

    private static string FormatResult(string label, LeftoverCleanupResult result)
        => $"{label}: quiesced {result.QuiescedFacilityIds.Count}/{result.QuiesceCandidateCount}, torn down {result.TornDownFacilityIds.Count}/{result.TeardownCandidateCount}, purged {result.PurgedRunIds.Count}/{result.HistoryPurgeCandidateCount}, failed facilities {result.FailedFacilityIds.Count}, failed runs {result.FailedRunIds.Count}.";

    private sealed class LoggerAutomationOutput(ILogger logger, string facilityId) : IAutomationOutput
    {
        public void WriteLine(string message) =>
            logger.LogInformation("Leftover cleanup {FacilityId}: {Message}", facilityId, message);

        public void WriteLine(string format, params object[] args) =>
            WriteLine(string.Format(format, args));
    }
}

public sealed record LeftoverCleanupResult(
    int QuiesceCandidateCount,
    IReadOnlyList<string> QuiescedFacilityIds,
    int TeardownCandidateCount,
    IReadOnlyList<string> TornDownFacilityIds,
    int HistoryPurgeCandidateCount,
    IReadOnlyList<Guid> PurgedRunIds,
    IReadOnlyList<string> FailedFacilityIds,
    IReadOnlyList<Guid> FailedRunIds);
