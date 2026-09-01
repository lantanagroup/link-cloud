using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Automation.Link.Models;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Interfaces;
using Automation.UI.Services.Persistence;

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
    ILogger<LeftoverRunCleanupService> logger) : BackgroundService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset? _lastQuiesceAt;

    public DateTimeOffset? LastQuiesceAt => _lastQuiesceAt;
    public LeftoverCleanupResult? LastQuiesceResult { get; private set; }
    public LeftoverCleanupResult? LastTeardownResult { get; private set; }
    public LeftoverCleanupResult? LastHistoryPurgeResult { get; private set; }

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

    public Task<LeftoverCleanupResult> RunQuiesceNowAsync(CancellationToken cancellationToken = default)
        => RunQuiesceAsync(cancellationToken);

    public Task<LeftoverCleanupResult> RunTeardownNowAsync(CancellationToken cancellationToken = default)
        => RunTeardownAsync(cancellationToken);

    public Task<LeftoverCleanupResult> RunHistoryPurgeNowAsync(CancellationToken cancellationToken = default)
        => RunHistoryPurgeAsync(cancellationToken);

    public Task<LeftoverCleanupResult> RunCustomRangeAsync(
        DateTimeOffset fromInclusiveUtc,
        DateTimeOffset toExclusiveUtc,
        bool teardownFacilities,
        bool purgeHistory,
        CancellationToken cancellationToken = default)
        => RunScopedAsync(
            "custom-range",
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
                    var result = await RunQuiesceAsync(stoppingToken);
                    _lastQuiesceAt = now;
                    LastQuiesceResult = result;
                }

                if (settings.Enabled && settings.DailyTeardownEnabled
                    && CleanupSchedule.IsDueDaily(now, settings.DailyTeardownTimeUtc, settings.CatchUpWindow, settings.LastDailyTeardownAt))
                {
                    var result = await RunTeardownAsync(stoppingToken);
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
                    var result = await RunHistoryPurgeAsync(stoppingToken);
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
        int? maxFacilitiesOverride = null)
        => RunScopedAsync(
            "quiesce",
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
        int? maxFacilitiesOverride = null)
        => RunScopedAsync(
            "teardown",
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
        int? maxFacilitiesOverride = null)
        => RunScopedAsync(
            "history-purge",
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
        await _gate.WaitAsync(cancellationToken);
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

            var facilitiesResponse = await facilityClient.GetFacilityListAsync(cancellationToken: cancellationToken);
            var facilities = facilitiesResponse.Body ?? new Dictionary<string, string>();
            var runs = await snapshotStore.GetAllRunSummariesAsync(since: null, ct: cancellationToken);
            var now = time.GetUtcNow();
            var (facilityIds, historyRuns) = select(facilities, runs, now, settings);

            var limit = Math.Max(1, maxFacilitiesOverride ?? settings.MaxFacilitiesPerPass);
            var quiesced = new List<string>();
            var tornDown = new List<string>();
            var purged = new List<Guid>();
            var failedFacilities = new List<string>();
            var failedRuns = new List<Guid>();

            foreach (var facilityId in facilityIds.Take(limit))
            {
                cancellationToken.ThrowIfCancellationRequested();
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
            }

            if (purgeHistory)
            {
                var historyLimit = Math.Max(limit, 200);
                foreach (var run in historyRuns.Take(historyLimit))
                {
                    cancellationToken.ThrowIfCancellationRequested();
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
                }
            }

            if (quiesced.Count > 0 || tornDown.Count > 0 || purged.Count > 0 || facilityIds.Count > 0 || historyRuns.Count > 0)
            {
                logger.LogInformation(
                    "Leftover Automation {Mode} finished. facilities={FacilityCandidates}, quiesced={Quiesced}, tornDown={TornDown}, history={HistoryCandidates}, purged={Purged}, failedFacilities={FailedFacilities}, failedRuns={FailedRuns}",
                    mode, facilityIds.Count, quiesced.Count, tornDown.Count, historyRuns.Count, purged.Count, failedFacilities.Count, failedRuns.Count);
            }

            return new LeftoverCleanupResult(
                facilityIds.Count,
                quiesced,
                facilityIds.Count,
                tornDown,
                historyRuns.Count,
                purged,
                failedFacilities,
                failedRuns);
        }
        finally
        {
            _gate.Release();
        }
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
