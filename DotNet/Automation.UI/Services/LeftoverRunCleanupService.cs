using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Automation.Link.Models;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Interfaces;
using Automation.UI.Services.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

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
    ICleanupReportStore reportStore,
    IPipelineAbortRegistry abortRegistry,
    IHubContext<CleanupHub> cleanupHub,
    IOptions<LeftoverRunCleanupOptions> leftoverOptions,
    ILogger<LeftoverRunCleanupService> logger) : BackgroundService, ILeftoverRunCleanup
{
    private static readonly TimeSpan TerminalPersistenceBudget = TimeSpan.FromSeconds(5);
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
        CancellationToken cancellationToken = default,
        bool deactivateSchedules = true)
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
            cancellationToken,
            deactivateSchedules);
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
                    ? RunCleanupHelper.SelectAutomationFacilitiesForRuns(facilities, ranged, runs)
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
            var startupDelay = leftoverOptions.Value.StartupDelay;
            if (startupDelay < TimeSpan.Zero)
                startupDelay = TimeSpan.Zero;
            await Task.Delay(startupDelay, stoppingToken);
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
                    LastQuiesceResult = result;
                    // A capped pass used to leave this unset, so the 30s sweep tick
                    // started another pass and wrote another report. Honor the interval.
                    _lastQuiesceAt = now;
                    if (!result.ProcessedAllCandidates)
                    {
                        logger.LogInformation(
                            "Scheduled quiesce left remaining leftover facilities; next attempt waits for the quiesce interval. candidates={Candidates}, quiesced={Quiesced}",
                            result.QuiesceCandidateCount, result.QuiescedFacilityIds.Count);
                    }
                }

                if (settings.Enabled && settings.DailyTeardownEnabled
                    && CleanupSchedule.IsDueDaily(now, settings.DailyTeardownTimeUtc, settings.CatchUpWindow, settings.LastDailyTeardownAt))
                {
                    var result = await RunTeardownAsync(stoppingToken, trigger: "scheduled");
                    LastTeardownResult = result;
                    if (result.Succeeded && result.ProcessedAllCandidates)
                    {
                        await settingsStore.RecordDailyTeardownAsync(now, FormatResult("Daily teardown", result), stoppingToken);
                    }
                    else
                    {
                        logger.LogWarning(
                            "Daily leftover teardown did not finish every candidate; not recording success so it can retry in the catch-up window. candidates={Candidates}, tornDown={TornDown}, failedFacilities={FailedFacilities}, failedRuns={FailedRuns}",
                            result.TeardownCandidateCount, result.TornDownFacilityIds.Count, result.FailedFacilityIds.Count, result.FailedRunIds.Count);
                    }
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
                    if (result.Succeeded && result.ProcessedAllCandidates)
                    {
                        await settingsStore.RecordWeeklyPurgeAsync(now, FormatResult("Weekly history purge", result), stoppingToken);
                    }
                    else
                    {
                        logger.LogWarning(
                            "Weekly leftover history purge did not finish every candidate; not recording success so it can retry in the catch-up window. candidates={Candidates}, purged={Purged}, failedFacilities={FailedFacilities}, failedRuns={FailedRuns}",
                            result.HistoryPurgeCandidateCount, result.PurgedRunIds.Count, result.FailedFacilityIds.Count, result.FailedRunIds.Count);
                    }
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
                var history = RunCleanupHelper.SelectHistoryPurgeRuns(runs, now, settings.TeardownRetention);
                return ([], history);
            },
            cancellationToken,
            maxFacilitiesOverride,
            teardownFacilities: false,
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
        var startedAt = time.GetUtcNow();
        var quiesced = new List<string>();
        var tornDown = new List<string>();
        var purged = new List<Guid>();
        var failedFacilities = new List<string>();
        var failedRuns = new List<Guid>();
        var quiesceCandidateCount = 0;
        var teardownCandidateCount = 0;
        var historyCandidateCount = 0;
        var savedTerminal = false;
        try
        {
            // The start request reads CurrentActivity as soon as this task hits its first I/O.
            // Publish running first so that response is not the previous pass's terminal activity.
            await PublishAsync(new CleanupActivity
            {
                Mode = mode,
                Label = label,
                Status = "running",
                Trigger = trigger,
                Message = "Starting leftover cleanup…",
                At = startedAt
            }, cancellationToken);

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
            var facilities = RunCleanupHelper.RequireFacilityList(facilitiesResponse);
            var runs = await snapshotStore.GetAllRunSummariesAsync(since: null, ct: cancellationToken);
            var now = time.GetUtcNow();
            var (facilityIds, historyRuns) = select(facilities, runs, now, settings);

            IReadOnlyList<string> selectedFacilities = facilityIds;
            if (!teardownFacilities)
            {
                var stillHot = new List<string>();
                foreach (var id in facilityIds)
                {
                    if (!await abortRegistry.IsAbortedAsync(id, reportId: null, cancellationToken))
                        stillHot.Add(id);
                }

                selectedFacilities = stillHot;
            }

            var sweepRetained = mode is "teardown" or "history-purge";
            var retainedEligible = sweepRetained
                ? await SelectRetainedFacilitiesAsync(facilities, runs, cancellationToken)
                : [];
            var limit = Math.Max(1, maxFacilitiesOverride ?? settings.MaxFacilitiesPerPass);
            var facilityWork = selectedFacilities.Take(limit).ToList();
            var retainedWork = retainedEligible
                .Where(id => !facilityWork.Exists(existing => string.Equals(existing, id, StringComparison.OrdinalIgnoreCase)))
                .Take(Math.Max(0, limit - facilityWork.Count))
                .ToList();
            var historyWork = new List<AutomationRunSummary>();
            if (purgeHistory)
            {
                var spent = facilityWork.Count + retainedWork.Count;
                var scheduledTeardown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (teardownFacilities)
                {
                    foreach (var id in facilityWork)
                        scheduledTeardown.Add(id);
                }
                foreach (var id in retainedWork)
                    scheduledTeardown.Add(id);

                foreach (var run in historyRuns)
                {
                    var owned = OwnedAutomationFacilityIds(run);
                    var extra = owned.Count(id => !scheduledTeardown.Contains(id));
                    if (historyWork.Count > 0 && spent + extra > limit)
                        break;

                    historyWork.Add(run);
                    foreach (var id in owned)
                        scheduledTeardown.Add(id);
                    spent += extra;
                }
            }
            var purgingRunIds = historyWork.Select(run => run.RunId).ToHashSet();
            var heldByOtherRun = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var other in runs)
            {
                if (purgingRunIds.Contains(other.RunId))
                    continue;
                if (!string.IsNullOrWhiteSpace(other.FacilityId))
                    heldByOtherRun.Add(other.FacilityId);
                heldByOtherRun.Add(other.RunId.ToString());
            }
            var total = facilityWork.Count + retainedWork.Count + historyWork.Count;
            var processed = 0;
            quiesceCandidateCount = teardownFacilities ? 0 : selectedFacilities.Count;
            teardownCandidateCount = CountTeardownAttempts(
                teardownFacilities, facilityWork, retainedWork, historyWork, heldByOtherRun);
            historyCandidateCount = historyRuns.Count;

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
                        if (retainedEligible.Exists(id => string.Equals(id, facilityId, StringComparison.OrdinalIgnoreCase)))
                            await snapshotStore.ReleaseRetainedFacilityAsync(facilityId, cancellationToken);
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
                catch (Exception ex) when (ex is not OperationCanceledException)
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
                    var output = new LoggerAutomationOutput(logger, run.FacilityId ?? run.RunId.ToString());
                    // Weekly history-purge always wants per-run facility teardown.
                    // Custom-range honors the independent teardownFacilities checkbox; skip IDs the facility loop already handled.
                    var teardownInPurge = mode == "history-purge" || teardownFacilities;
                    // FacilityId and the scenario RunId are both Automation-owned facility ids.
                    // Keep the run snapshot when a required teardown fails so a later pass can retry.
                    var pendingTeardown = teardownInPurge
                        ? OwnedAutomationFacilityIds(run).Where(id => IsNewHistoryTeardown(id, tornDown)).ToList()
                        : [];
                    var teardownFailed = false;
                    foreach (var facilityId in pendingTeardown)
                    {
                        if (heldByOtherRun.Contains(facilityId))
                        {
                            logger.LogInformation(
                                "History purge left facility {FacilityId} in place because another run still references it.",
                                facilityId);
                            continue;
                        }

                        try
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
                            // A custom-range facility pass may already have recorded this id. A successful retry clears it.
                            failedFacilities.RemoveAll(id => string.Equals(id, facilityId, StringComparison.OrdinalIgnoreCase));
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            logger.LogWarning(ex, "History purge facility teardown failed for {FacilityId}.", facilityId);
                            if (!failedFacilities.Exists(id => string.Equals(id, facilityId, StringComparison.OrdinalIgnoreCase)))
                                failedFacilities.Add(facilityId);
                            teardownFailed = true;
                        }
                    }

                    if (teardownFailed)
                    {
                        failedRuns.Add(run.RunId);
                    }
                    else
                    {
                        // Owned ids were already torn down above. Do not let the helper fall back to run.FacilityId.
                        await RunCleanupHelper.PurgeRunHistoryAsync(
                            facilityClient,
                            normalizationClient,
                            dataAcqClient,
                            queryDispatchClient,
                            censusClient,
                            reportClient,
                            abortRegistry,
                            snapshotStore,
                            output,
                            run,
                            settings.AbortTtl,
                            cancellationToken,
                            teardownFacility: false);
                        purged.Add(run.RunId);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "History purge failed for run {RunId}.", run.RunId);
                    failedRuns.Add(run.RunId);
                }

                processed++;
            }

            foreach (var facilityId in retainedWork)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await PublishProgressAsync(
                    mode, label, trigger, total, processed, quiesced, tornDown, purged, failedFacilities, failedRuns,
                    "Tearing down leftover facility", facilityId, cancellationToken);
                try
                {
                    await TearDownOneFacilityAsync(
                        facilityClient, normalizationClient, dataAcqClient, queryDispatchClient,
                        censusClient, reportClient, abortRegistry, settings, facilityId, cancellationToken);
                    tornDown.Add(facilityId);
                    failedFacilities.RemoveAll(id => string.Equals(id, facilityId, StringComparison.OrdinalIgnoreCase));
                    await snapshotStore.ReleaseRetainedFacilityAsync(facilityId, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Retained facility teardown failed for {FacilityId}.", facilityId);
                    if (!failedFacilities.Exists(id => string.Equals(id, facilityId, StringComparison.OrdinalIgnoreCase)))
                        failedFacilities.Add(facilityId);
                }

                processed++;
            }

            var result = new LeftoverCleanupResult(
                quiesceCandidateCount,
                quiesced,
                teardownCandidateCount,
                tornDown,
                historyCandidateCount,
                purged,
                failedFacilities,
                failedRuns);

            if (quiesced.Count > 0 || tornDown.Count > 0 || purged.Count > 0 || selectedFacilities.Count > 0 || historyRuns.Count > 0)
            {
                logger.LogInformation(
                    "Leftover Automation {Mode} finished. facilities={FacilityCandidates}, quiesced={Quiesced}, tornDown={TornDown}, history={HistoryCandidates}, purged={Purged}, failedFacilities={FailedFacilities}, failedRuns={FailedRuns}, processedAll={ProcessedAll}",
                    mode, selectedFacilities.Count, quiesced.Count, tornDown.Count, historyRuns.Count, purged.Count, failedFacilities.Count, failedRuns.Count, result.ProcessedAllCandidates);
            }

            var status = failedFacilities.Count > 0 || failedRuns.Count > 0 ? "failed" : "completed";
            var finishedAt = time.GetUtcNow();
            var message = FormatActivityResult(label, result);
            var persisted = await SaveReportWithinBudgetAsync(new CleanupReport
            {
                Id = Guid.NewGuid(),
                Mode = mode,
                Label = label,
                Trigger = trigger,
                Status = status,
                StartedAt = startedAt,
                FinishedAt = finishedAt,
                QuiesceCandidateCount = result.QuiesceCandidateCount,
                QuiescedFacilityIds = result.QuiescedFacilityIds,
                TeardownCandidateCount = result.TeardownCandidateCount,
                TornDownFacilityIds = result.TornDownFacilityIds,
                HistoryPurgeCandidateCount = result.HistoryPurgeCandidateCount,
                PurgedRunIds = result.PurgedRunIds,
                FailedFacilityIds = result.FailedFacilityIds,
                FailedRunIds = result.FailedRunIds,
                Message = message
            }, cancellationToken);
            savedTerminal = persisted;
            if (!persisted)
                message += " The cleanup report could not be saved.";

            try
            {
                await PublishWithinBudgetAsync(new CleanupActivity
                {
                    Mode = mode,
                    Label = label,
                    Status = status,
                    Trigger = trigger,
                    Total = total,
                    Processed = processed,
                    Quiesced = quiesced.Count,
                    TornDown = tornDown.Count,
                    Purged = purged.Count,
                    Failed = failedFacilities.Count + failedRuns.Count,
                    Message = message,
                    At = finishedAt
                }, cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // The report is already stored. A notification-budget timeout must not write a second failed report.
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            if (savedTerminal)
                throw;

            var finishedAt = time.GetUtcNow();
            var message = $"{label} cancelled.";
            await SaveBestEffortWithinBudgetAsync(PartialReport(
                mode, label, trigger, startedAt, finishedAt, message,
                quiesceCandidateCount, quiesced, teardownCandidateCount, tornDown,
                historyCandidateCount, purged, failedFacilities, failedRuns));
            await PublishBestEffortWithinBudgetAsync(CurrentActivity with
            {
                Status = "failed",
                Message = message,
                At = finishedAt
            });
            throw;
        }
        catch (Exception ex)
        {
            var finishedAt = time.GetUtcNow();
            var message = $"{label} failed: {ex.Message}";
            await SaveBestEffortWithinBudgetAsync(PartialReport(
                mode, label, trigger, startedAt, finishedAt, message,
                quiesceCandidateCount, quiesced, teardownCandidateCount, tornDown,
                historyCandidateCount, purged, failedFacilities, failedRuns));
            await PublishBestEffortWithinBudgetAsync(new CleanupActivity
            {
                Mode = mode,
                Label = label,
                Status = "failed",
                Trigger = trigger,
                Message = message,
                At = finishedAt
            });
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

    private static int CountTeardownAttempts(
        bool teardownFacilities,
        IReadOnlyList<string> facilityWork,
        IReadOnlyList<string> retainedWork,
        IReadOnlyList<AutomationRunSummary> historyWork,
        HashSet<string> heldByOtherRun)
    {
        var attempted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (teardownFacilities)
        {
            foreach (var id in facilityWork)
                attempted.Add(id);
        }

        foreach (var id in retainedWork)
            attempted.Add(id);

        foreach (var run in historyWork)
        {
            foreach (var id in OwnedAutomationFacilityIds(run))
            {
                if (!heldByOtherRun.Contains(id))
                    attempted.Add(id);
            }
        }

        return attempted.Count;
    }

    private static int TeardownCandidateCount(
        string mode,
        bool teardownFacilities,
        bool purgeHistory,
        IReadOnlyList<string> facilityIds,
        IReadOnlyList<AutomationRunSummary> historyRuns)
    {
        if (mode == "history-purge")
            return HistoryTeardownCandidates(historyRuns).Count;

        if (!teardownFacilities)
            return 0;

        var ids = facilityIds.AsEnumerable();
        if (purgeHistory)
            ids = ids.Concat(HistoryTeardownCandidates(historyRuns));

        return ids.Distinct(StringComparer.OrdinalIgnoreCase).Count();
    }

    private static bool IsNewHistoryTeardown(string? facilityId, List<string> tornDown)
        => !string.IsNullOrWhiteSpace(facilityId)
           && RunCleanupHelper.IsAutomationFacilityId(facilityId)
           && !tornDown.Exists(id => string.Equals(id, facilityId, StringComparison.OrdinalIgnoreCase));

    private static List<string> HistoryTeardownCandidates(IReadOnlyList<AutomationRunSummary> historyRuns)
        => historyRuns
            .SelectMany(OwnedAutomationFacilityIds)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private async Task<List<string>> SelectRetainedFacilitiesAsync(
        IReadOnlyDictionary<string, string> facilities,
        IReadOnlyList<AutomationRunSummary> runs,
        CancellationToken cancellationToken)
    {
        var retained = await snapshotStore.GetRetainedFacilityIdsAsync(cancellationToken) ?? [];
        var eligible = new List<string>();
        foreach (var facilityId in retained)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(facilityId))
                continue;

            var stillReferenced = runs.Any(run =>
                string.Equals(run.FacilityId, facilityId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(run.RunId.ToString(), facilityId, StringComparison.OrdinalIgnoreCase));
            if (stillReferenced)
                continue;

            if (!facilities.ContainsKey(facilityId))
            {
                await snapshotStore.ReleaseRetainedFacilityAsync(facilityId, cancellationToken);
                continue;
            }

            eligible.Add(facilityId);
        }

        return eligible;
    }

    private async Task TearDownOneFacilityAsync(
        IFacilityServiceClient facilityClient,
        INormalizationServiceClient normalizationClient,
        IDataAcquisitionServiceClient dataAcqClient,
        IQueryDispatchServiceClient queryDispatchClient,
        ICensusServiceClient censusClient,
        IReportServiceClient reportClient,
        IPipelineAbortRegistry abortRegistry,
        LeftoverRunCleanupSettings settings,
        string facilityId,
        CancellationToken cancellationToken)
    {
        var output = new LoggerAutomationOutput(logger, facilityId);
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
    }

    private static List<string> OwnedAutomationFacilityIds(AutomationRunSummary run)
    {
        var ids = new List<string>();
        Add(run.FacilityId);
        Add(run.RunId.ToString());
        return ids;

        void Add(string? id)
        {
            if (!RunCleanupHelper.IsOwnedAutomationFacilityId(run, id) || id is null)
                return;
            if (ids.Exists(existing => string.Equals(existing, id, StringComparison.OrdinalIgnoreCase)))
                return;
            ids.Add(id);
        }
    }

    private async Task<bool> SaveReportWithinBudgetAsync(CleanupReport report, CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(TerminalPersistenceBudget);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            return await TrySaveReportAsync(report, linked.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Cleanup report save exceeded {Budget}.", TerminalPersistenceBudget);
            return false;
        }
    }

    private async Task PublishWithinBudgetAsync(CleanupActivity activity, CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(TerminalPersistenceBudget);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            await PublishAsync(activity, linked.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Cleanup activity publish exceeded {Budget}.", TerminalPersistenceBudget);
        }
    }

    private Task SaveBestEffortWithinBudgetAsync(CleanupReport report)
        => SaveReportWithinBudgetAsync(report, CancellationToken.None);

    private Task PublishBestEffortWithinBudgetAsync(CleanupActivity activity)
        => PublishWithinBudgetAsync(activity, CancellationToken.None);

    private static CleanupReport PartialReport(
        string mode,
        string label,
        string trigger,
        DateTimeOffset startedAt,
        DateTimeOffset finishedAt,
        string message,
        int quiesceCandidateCount,
        List<string> quiesced,
        int teardownCandidateCount,
        List<string> tornDown,
        int historyCandidateCount,
        List<Guid> purged,
        List<string> failedFacilities,
        List<Guid> failedRuns)
        => new()
        {
            Id = Guid.NewGuid(),
            Mode = mode,
            Label = label,
            Trigger = trigger,
            Status = "failed",
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            QuiesceCandidateCount = quiesceCandidateCount,
            QuiescedFacilityIds = quiesced,
            TeardownCandidateCount = teardownCandidateCount,
            TornDownFacilityIds = tornDown,
            HistoryPurgeCandidateCount = historyCandidateCount,
            PurgedRunIds = purged,
            FailedFacilityIds = failedFacilities,
            FailedRunIds = failedRuns,
            Message = message
        };

    private async Task<bool> TrySaveReportAsync(CleanupReport report, CancellationToken cancellationToken)
    {
        try
        {
            await reportStore.SaveAsync(report, cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not persist leftover cleanup report for {Mode}.", report.Mode);
            return false;
        }
    }

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
    IReadOnlyList<Guid> FailedRunIds)
{
    public bool Succeeded => FailedFacilityIds.Count == 0 && FailedRunIds.Count == 0;

    public bool ProcessedAllCandidates =>
        (QuiesceCandidateCount == 0 || QuiescedFacilityIds.Count + FailedFacilityIds.Count == QuiesceCandidateCount)
        && (TeardownCandidateCount == 0 || TornDownFacilityIds.Count + FailedFacilityIds.Count == TeardownCandidateCount)
        && (HistoryPurgeCandidateCount == 0 || PurgedRunIds.Count + FailedRunIds.Count == HistoryPurgeCandidateCount);
}
