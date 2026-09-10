using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartz;
using System.Diagnostics;
using System.Text;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.DataAcquisition.Jobs;

[DisallowConcurrentExecution]
public class AcquisitionProcessingJob : IJob
{
    private readonly ILogger<AcquisitionProcessingJob> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IProducer<long, ReadyToAcquire> _readyToAcquireProducer;
    private readonly AcquisitionWorkerProcessorSettings _settings;
    private const int BatchSize = 100;

    public AcquisitionProcessingJob(
        ILogger<AcquisitionProcessingJob> logger,
        IServiceScopeFactory serviceScopeFactory,
        IProducer<long, ReadyToAcquire> readyToAcquireProducer,
        IOptions<AcquisitionWorkerProcessorSettings> settings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _readyToAcquireProducer = readyToAcquireProducer ?? throw new ArgumentNullException(nameof(readyToAcquireProducer));
        _settings = settings?.Value ?? new AcquisitionWorkerProcessorSettings();
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await FailStalledQueuedLogs(context.CancellationToken);
            await ResetStalledProcessingLogs(context.CancellationToken);

            // Start the stopwatch AFTER housekeeping so the full time budget
            // is available for the primary work of dispatching pending logs.
            var stopwatch = Stopwatch.StartNew();
            await ProcessPendingLogs(stopwatch, context.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in AcquisitionProcessingJob.");
            throw new JobExecutionException(ex, refireImmediately: false);
        }
    }

    private async Task FailStalledQueuedLogs(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dataAcquisitionLogManager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();

            int failedCount = await dataAcquisitionLogManager.FailStalledQueuedLogsAsync(_settings.StalledQueuedThresholdMinutes, _settings.MaxBatchesFailStalledPerRun, cancellationToken);
            if (failedCount > 0)
            {
                _logger.LogInformation("Successfully failed {count} stalled queued logs.", failedCount);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Failing stalled queued logs operation was cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while failing stalled queued logs.");
        }
    }
    
    private async Task ResetStalledProcessingLogs(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dataAcquisitionLogManager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();

            int resetCount = await dataAcquisitionLogManager.ResetStalledProcessingLogsAsync(_settings.StalledProcessingThresholdMinutes, _settings.MaxBatchesFailStalledPerRun, cancellationToken);

            if (resetCount > 0)
            {
                _logger.LogInformation("Successfully reset {count} stalled processing logs to Pending.", resetCount);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Resetting stalled processing logs operation was cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while resetting stalled processing logs.");
        }
    }

    public async Task ProcessPendingLogs(Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dataAcquisitionLogQueries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();
            var facilities = await dataAcquisitionLogQueries.GetFacilitiesWithPendingAndRetryableFailedRequests(cancellationToken);

            foreach (var facilityId in facilities)
            {
                if (stopwatch.Elapsed.TotalSeconds >= _settings.TimeBudgetPerRunSeconds)
                {
                    _logger.LogInformation("ProcessPendingLogs reached time budget of {budget}s. Skipping remaining facilities.", _settings.TimeBudgetPerRunSeconds);
                    break;
                }

                await ProcessFacilityPendingLogs(facilityId, stopwatch, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Processing pending logs operation was cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving facilities for processing pending logs.");
        }
    }

    private async Task ProcessFacilityPendingLogs(string facilityId, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var abortRegistry = scope.ServiceProvider.GetService<IPipelineAbortRegistry>();
            if (abortRegistry != null &&
                await abortRegistry.IsAbortedAsync(facilityId, reportId: null, cancellationToken))
            {
                _logger.LogInformation("Skipping pending logs for aborted facility {FacilityId}.", facilityId.SanitizeForLog());
                return;
            }

            var dataAcquisitionLogManager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();
            var fhirQueryConfigurationQueries = scope.ServiceProvider.GetRequiredService<IFhirQueryConfigurationQueries>();
            var dataAcquisitionLogQueries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

            var config = await fhirQueryConfigurationQueries.GetByFacilityIdAsync(facilityId, cancellationToken);

            List<RequestStatus> statuses = [RequestStatus.Failed, RequestStatus.Pending];
            var dateTimeNow = DateTime.UtcNow;

            if (config == null)
            {
                _logger.LogCritical("Request FAILED due to missing FhirQueryConfiguration. FacilityId: {facilityId}", facilityId.Sanitize());

                long? lastMissingConfigId = null;
                int batchesProcessedMissing = 0;
                while (batchesProcessedMissing < _settings.MaxBatchesPerFacilityPerRun)
                {
                    if (stopwatch.Elapsed.TotalSeconds >= _settings.TimeBudgetPerRunSeconds)
                    {
                        _logger.LogInformation("ProcessFacilityPendingLogs (MissingConfig) for facility {facilityId} reached time budget of {budget}s. Yielding.", facilityId.SanitizeForLog(), _settings.TimeBudgetPerRunSeconds.SanitizeForLog());
                        break;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    var requests = await dataAcquisitionLogQueries.GetNextEligibleBatchForFacility(facilityId, lastMissingConfigId, BatchSize, statuses, dateTimeNow, cancellationToken);
                    if (!requests.Any()) break;

                    // Missing config is a configuration error, not a transient failure.
                    // Move all affected logs directly to the terminal ConfigurationMissing status
                    // so they are never re-queued, and record the reason as a note.
                    var allIds = requests.Select(r => r.Id).ToList();
                    await dataAcquisitionLogManager.SetConfigurationMissingWithNoteBatchAsync(
                        allIds,
                        "FhirQueryConfiguration not found for this facility.",
                        cancellationToken);

                    lastMissingConfigId = requests.Last().Id;
                    batchesProcessedMissing++;
                }

                if (batchesProcessedMissing >= _settings.MaxBatchesPerFacilityPerRun)
                {
                    _logger.LogInformation("ProcessFacilityPendingLogs (MissingConfig) for facility {facilityId} reached max batch limit of {maxBatches}. Yielding.", facilityId.SanitizeForLog(), _settings.MaxBatchesPerFacilityPerRun.SanitizeForLog());
                }

                return;
            }

            if (!IsWithinAcquisitionWindow(config.MinAcquisitionPullTime, config.MaxAcquisitionPullTime))
            {
                _logger.LogInformation("Current time {currentTime} is outside the acquisition window for facility {facilityId}.", dateTimeNow.TimeOfDay, facilityId.SanitizeForLog());
                return;
            }

            long? lastId = null;
            int batchesProcessed = 0;
            while (batchesProcessed < _settings.MaxBatchesPerFacilityPerRun)
            {
                if (stopwatch.Elapsed.TotalSeconds >= _settings.TimeBudgetPerRunSeconds)
                {
                    _logger.LogInformation("ProcessFacilityPendingLogs for facility {facilityId} reached time budget of {budget}s. Yielding.", facilityId.SanitizeForLog(), _settings.TimeBudgetPerRunSeconds.SanitizeForLog());
                    break;
                }

                cancellationToken.ThrowIfCancellationRequested();
                _logger.LogInformation("Fetching batch after Id {lastId} for facility {facilityId}", lastId.SanitizeForLog() ?? "null", facilityId.SanitizeForLog());
                var requests = await dataAcquisitionLogQueries.GetNextEligibleBatchForFacility(facilityId, lastId, BatchSize, statuses, dateTimeNow, cancellationToken);
                if (!requests.Any())
                {
                    _logger.LogInformation("No more logs to process for facility {facilityId}", facilityId.SanitizeForLog());
                    break;
                }

                _logger.BeginScope("Processing {count} processable requests for facility {facilityId}", requests.Count, facilityId);

                var failedLogs = requests.Where(r => r.Status == RequestStatus.Failed).ToList();

                var maxRetryAttempts = config.MaxRetries ?? DataAcquisitionLog.MaxRetryAttempts;

                var maxRetriesReachedIds = failedLogs
                    .Where(r => r.RetryAttempts >= maxRetryAttempts)
                    .Select(r => r.Id)
                    .ToList();

                var retryableFailedLogIds = failedLogs
                    .Where(r => !maxRetriesReachedIds.Contains(r.Id))
                    .Select(r => r.Id)
                    .ToList();
                var pendingLogIds = requests
                    .Where(r => r.Status == RequestStatus.Pending)
                    .Select(r => r.Id)
                    .ToList();

                if (maxRetriesReachedIds.Any())
                {
                    await dataAcquisitionLogManager.UpdateStatusBatchAsync(maxRetriesReachedIds, RequestStatus.MaxRetriesReached, false, cancellationToken);
                }

                if (pendingLogIds.Any())
                {
                    await dataAcquisitionLogManager.UpdateStatusBatchAsync(pendingLogIds, RequestStatus.Ready, false, cancellationToken);
                }

                if (retryableFailedLogIds.Any())
                {
                    await dataAcquisitionLogManager.UpdateStatusBatchAsync(retryableFailedLogIds, RequestStatus.Ready, true, cancellationToken);
                }

                foreach (var request in requests)
                {
                    if (maxRetriesReachedIds.Contains(request.Id)) continue;

                    if (abortRegistry != null &&
                        await abortRegistry.IsAbortedAsync(null, request.ReportTrackingId, cancellationToken))
                    {
                        _logger.LogInformation(
                            "Skipping ReadyToAcquire for aborted report {ReportTrackingId}, log {LogId}.",
                            request.ReportTrackingId, request.Id);
                        continue;
                    }

                    try
                    {
                        _logger.LogDebug("Producing ReadyToAcquire message for log id: {logId} and facility id: {facilityId}", request.Id, facilityId.Sanitize());

                        var headers = new Headers
                        {
                            { "X-Correlation-Id", Encoding.UTF8.GetBytes(request.CorrelationId?.ToString() ?? string.Empty) }
                        };

                        await _readyToAcquireProducer.ProduceAsync(
                            KafkaTopic.ReadyToAcquire.ToString(),
                            new Message<long, ReadyToAcquire>
                            {
                                Key = request.Id,
                                Value = new ReadyToAcquire
                                {
                                    LogId = request.Id,
                                    FacilityId = facilityId,
                                    ReportTrackingId = request.ReportTrackingId
                                },
                                Headers = headers
                            }, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error producing ReadyToAcquire message for log id: {logId}", request.Id);
                        await dataAcquisitionLogManager.UpdateStatusBatchAsync([request.Id], RequestStatus.Failed, false, cancellationToken);
                    }
                }
                _readyToAcquireProducer.Flush(cancellationToken);

                lastId = requests.Last().Id;
                batchesProcessed++;
            }

            if (batchesProcessed >= _settings.MaxBatchesPerFacilityPerRun)
            {
                _logger.LogDebug("ProcessFacilityPendingLogs for facility {facilityId} reached max batch limit of {maxBatches}. Yielding.", facilityId.SanitizeForLog(), _settings.MaxBatchesPerFacilityPerRun.SanitizeForLog());
            }

            _logger.LogInformation("Completed processing processable requests for facility {facilityId}.", facilityId.SanitizeForLog());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing acquisition job for facility id: {facilityId}", facilityId.SanitizeForLog());
        }
    }

    private static bool IsWithinAcquisitionWindow(TimeSpan? minAcquisitionPullTime, TimeSpan? maxAcquisitionPullTime)
    {
        // No time restrictions
        if (minAcquisitionPullTime == default && maxAcquisitionPullTime == default)
        {
            return true;
        }

        var currentTime = DateTime.UtcNow.TimeOfDay;

        // Same-day window (e.g., 9 AM to 5 PM)
        if (minAcquisitionPullTime <= maxAcquisitionPullTime)
        {
            return currentTime >= minAcquisitionPullTime && currentTime <= maxAcquisitionPullTime;
        }

        // Midnight-spanning window (e.g., 8 PM to 4 AM)
        return currentTime >= minAcquisitionPullTime || currentTime <= maxAcquisitionPullTime;
    }

}
