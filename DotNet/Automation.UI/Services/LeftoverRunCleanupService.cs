using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Automation.Link.Models;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace Automation.UI.Services;

/// <summary>
/// Two-phase leftover cleanup: quickly stop work that is still moving (abort,
/// cancel DA, disable census), then tear down resting facility data after 14 days.
/// </summary>
public sealed class LeftoverRunCleanupService(
    IServiceScopeFactory scopeFactory,
    ISnapshotStore snapshotStore,
    TimeProvider time,
    IOptions<LeftoverRunCleanupOptions> options,
    IPipelineAbortRegistry abortRegistry,
    ILogger<LeftoverRunCleanupService> logger) : BackgroundService
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task QuiesceFacilityAsync(
        string? facilityId,
        string? reportId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            return;

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
            options.Value.AbortTtl,
            cancellationToken);
    }

    public async Task<LeftoverCleanupResult> RunOnceAsync(
        CancellationToken cancellationToken = default,
        int? maxFacilitiesOverride = null)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var settings = options.Value;
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

            var quiesceCandidates = RunCleanupHelper.SelectQuiesceAutomationFacilities(
                facilities, runs, now, settings.QuiesceGrace);
            var teardownCandidates = RunCleanupHelper.SelectTeardownAutomationFacilities(
                facilities, runs, now, settings.TeardownRetention);

            var limit = Math.Max(1, maxFacilitiesOverride ?? settings.MaxFacilitiesPerPass);
            var quiesced = new List<string>();
            var tornDown = new List<string>();
            var failures = new List<string>();

            foreach (var facilityId in quiesceCandidates.Take(limit))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var output = new LoggerAutomationOutput(logger, facilityId);
                try
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
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Leftover facility quiesce failed for {FacilityId}.", facilityId);
                    failures.Add(facilityId);
                }
            }

            var remaining = Math.Max(0, limit - quiesced.Count);
            foreach (var facilityId in teardownCandidates.Take(remaining))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var output = new LoggerAutomationOutput(logger, facilityId);
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
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Leftover facility teardown failed for {FacilityId}.", facilityId);
                    failures.Add(facilityId);
                }
            }

            if (quiesced.Count > 0 || tornDown.Count > 0 || quiesceCandidates.Count > 0 || teardownCandidates.Count > 0)
            {
                logger.LogInformation(
                    "Leftover Automation facility sweep finished. quiesceCandidates={QuiesceCandidates}, quiesced={Quiesced}, teardownCandidates={TeardownCandidates}, tornDown={TornDown}, failed={Failed}",
                    quiesceCandidates.Count, quiesced.Count, teardownCandidates.Count, tornDown.Count, failures.Count);
            }

            return new LeftoverCleanupResult(
                quiesceCandidates.Count,
                quiesced,
                teardownCandidates.Count,
                tornDown,
                failures);
        }
        finally
        {
            _gate.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            logger.LogInformation("Leftover Automation facility cleanup is disabled.");
            return;
        }

        try
        {
            await Task.Delay(settings.StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
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
                await Task.Delay(settings.Interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

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
    IReadOnlyList<string> FailedFacilityIds);
