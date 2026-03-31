using System.Diagnostics;
using System.Text;
using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.Options;
using Quartz;
using RequestStatus = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.DataAcquisition.Jobs;

[DisallowConcurrentExecution]
public class AcquisitionProcessingJob : IJob
{
    private readonly ILogger<AcquisitionProcessingJob> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IProducer<long, ReadyToAcquire> _readyToAcquireProducer;
    private readonly IProducer<ResourceKey, ResourceAcquired> _resourceAcquiredProducer;
    private readonly AcquisitionWorkerProcessorSettings _settings;
    private const int BatchSize = 25;

    public AcquisitionProcessingJob(
        ILogger<AcquisitionProcessingJob> logger,
        IServiceScopeFactory serviceScopeFactory,
        IProducer<long, ReadyToAcquire> readyToAcquireProducer,
        IProducer<ResourceKey, ResourceAcquired> resourceAcquiredProducer,
        IOptions<AcquisitionWorkerProcessorSettings> settings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _readyToAcquireProducer = readyToAcquireProducer ?? throw new ArgumentNullException(nameof(readyToAcquireProducer));
        _resourceAcquiredProducer = resourceAcquiredProducer ?? throw new ArgumentNullException(nameof(resourceAcquiredProducer));
        _settings = settings?.Value ?? new AcquisitionWorkerProcessorSettings();
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        await FailStalledQueuedLogs(context.CancellationToken);
        await ResetStalledProcessingLogs(context.CancellationToken);
        await ProcessPendingLogs(stopwatch, context.CancellationToken);
        await ProcessPendingTailingMessages(context.CancellationToken);
    }

    private async Task FailStalledQueuedLogs(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dataAcquisitionLogQueries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();
            
            int failedCount = await dataAcquisitionLogQueries.FailStalledQueuedLogsAsync(_settings.StalledQueuedThresholdMinutes, _settings.MaxBatchesFailStalledPerRun, cancellationToken);
            
            if (failedCount > 0)
            {
                _logger.LogInformation("Successfully failed {count} stalled queued logs.", failedCount);
            }
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
            var dataAcquisitionLogQueries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

            int resetCount = await dataAcquisitionLogQueries.ResetStalledProcessingLogsAsync(_settings.StalledProcessingThresholdMinutes, _settings.MaxBatchesFailStalledPerRun, cancellationToken);

            if (resetCount > 0)
            {
                _logger.LogInformation("Successfully reset {count} stalled processing logs to Ready.", resetCount);
            }
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

            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = _settings.MaxConcurrentAcquisitions, CancellationToken = cancellationToken };

            await Parallel.ForEachAsync(facilities, parallelOptions, async (facilityId, ct) =>
            {
                await ProcessFacilityPendingLogs(facilityId, stopwatch, cancellationToken);
            });
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
                        _logger.LogInformation("ProcessFacilityPendingLogs (MissingConfig) for facility {facilityId} reached time budget of {budget}s. Yielding.", facilityId, _settings.TimeBudgetPerRunSeconds);
                        break;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    var requests = await dataAcquisitionLogQueries.GetNextEligibleBatchForFacility(facilityId, lastMissingConfigId, BatchSize, statuses, dateTimeNow, cancellationToken);
                    if (!requests.Any()) break;

                    var logIds = requests.Select(r => r.Id).ToList();
                    await dataAcquisitionLogManager.UpdateStatusBatchAsync(logIds, RequestStatus.Failed, cancellationToken);

                    lastMissingConfigId = requests.Last().Id;
                    batchesProcessedMissing++;
                }

                if (batchesProcessedMissing >= _settings.MaxBatchesPerFacilityPerRun)
                {
                    _logger.LogInformation("ProcessFacilityPendingLogs (MissingConfig) for facility {facilityId} reached max batch limit of {maxBatches}. Yielding.", facilityId, _settings.MaxBatchesPerFacilityPerRun);
                }

                return;
            }

            if (!IsWithinAcquisitionWindow(config.MinAcquisitionPullTime, config.MaxAcquisitionPullTime))
            {
                _logger.LogInformation("Current time {currentTime} is outside the acquisition window for facility {facilityId}.", dateTimeNow.TimeOfDay, facilityId);
                return;
            }

            long? lastId = null;
            int batchesProcessed = 0;
            while (batchesProcessed < _settings.MaxBatchesPerFacilityPerRun)
            {
                if (stopwatch.Elapsed.TotalSeconds >= _settings.TimeBudgetPerRunSeconds)
                {
                    _logger.LogInformation("ProcessFacilityPendingLogs for facility {facilityId} reached time budget of {budget}s. Yielding.", facilityId, _settings.TimeBudgetPerRunSeconds);
                    break;
                }

                cancellationToken.ThrowIfCancellationRequested();
                _logger.LogInformation("Fetching batch after Id {lastId} for facility {facilityId}", lastId?.ToString() ?? "null", facilityId);
                var requests = await dataAcquisitionLogQueries.GetNextEligibleBatchForFacility(facilityId, lastId, BatchSize, statuses, dateTimeNow, cancellationToken);
                if (!requests.Any())
                {
                    _logger.LogInformation("No more logs to process for facility {facilityId}", facilityId);
                    break;
                }

                _logger.BeginScope("Processing {count} processable requests for facility {facilityId}", requests.Count, facilityId);

                var logIds = requests.Select(r => r.Id).ToList();
                var failedLogs = requests.Where(r => r.Status == RequestStatus.Failed).ToList();
                var maxRetriesReachedIds = failedLogs
                    .Where(r => r.RetryAttempts >= DataAcquisitionLog.MaxRetryAttempts)
                    .Select(r => r.Id)
                    .ToList();
                
                var retryableLogIds = logIds.Except(maxRetriesReachedIds).ToList();

                if (maxRetriesReachedIds.Any())
                {
                    await dataAcquisitionLogManager.UpdateStatusBatchAsync(maxRetriesReachedIds, RequestStatus.MaxRetriesReached, cancellationToken);
                }

                if (retryableLogIds.Any())
                {
                    // We can't easily increment RetryAttempts in ExecuteUpdateAsync if it's null or we need different notes per record
                    // But for the job, we can assume they all get +1 and the same note if we want true bulk
                    // However, some might be Pending (RetryAttempts 0) and some Failed (RetryAttempts > 0)
                    
                    // To keep it simple and safe for now, let's at least bulk update the status to Ready
                    await dataAcquisitionLogManager.UpdateStatusBatchAsync(retryableLogIds, RequestStatus.Ready, cancellationToken);
                }

                foreach (var request in requests)
                {
                    if (maxRetriesReachedIds.Contains(request.Id)) continue;

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
                        await dataAcquisitionLogManager.UpdateStatusBatchAsync([request.Id], RequestStatus.Failed, cancellationToken);
                    }
                }
                _readyToAcquireProducer.Flush(cancellationToken);

                lastId = requests.Last().Id;
                batchesProcessed++;
            }

            if (batchesProcessed >= _settings.MaxBatchesPerFacilityPerRun)
            {
                _logger.LogDebug("ProcessFacilityPendingLogs for facility {facilityId} reached max batch limit of {maxBatches}. Yielding.", facilityId, _settings.MaxBatchesPerFacilityPerRun);
            }

            _logger.LogInformation("Completed processing processable requests for facility {facilityId}.", facilityId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing acquisition job for facility id: {facilityId}", facilityId);
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

    public async Task ProcessPendingTailingMessages(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dataAcquisitionLogManager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();
        var dataAcquisitionLogQueries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        IEnumerable<TailingMessageModel> tailingMessages = null;
        try
        {
            tailingMessages = await dataAcquisitionLogQueries.GetTailingMessages(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An exception occurred while attempting to retrieve pending tail messages.");
            throw;
        }

        try
        {
            foreach (var message in tailingMessages)
            {
                try
                {
                    // Parse the traceparent string (format: 00-traceId-spanId-flags)
                    ActivityContext parentContext = CreateActivityContext(message.TraceParentId);
                    
                    // Start the activity with the parent context
                    using var activity = ServiceActivitySource.Instance?.StartActivity(
                        "ProcessTailingMessage", 
                        ActivityKind.Consumer, 
                        parentContext) ?? Activity.Current;

                    // Add relevant tags
                    activity?.SetTag("reportTrackingId", message.ResourceAcquired.ScheduledReports.FirstOrDefault()?.ReportTrackingId);
                    activity?.SetTag("facilityId", message.FacilityId);

                    var headers = new Headers
                    {
                        new Header(DataAcquisitionConstants.HeaderNames.CorrelationId,
                            Encoding.UTF8.GetBytes(message.CorrelationId))
                    };
                    
                    string currentTraceParent;
                    if (!string.IsNullOrEmpty(message.TraceParentId))
                    {
                        currentTraceParent = message.TraceParentId;
                    }
                    else if (activity is not null)
                    {
                        var ctx = activity.Context;
                        var flags = ctx.TraceFlags.HasFlag(ActivityTraceFlags.Recorded) ? "01" : "00";
                        currentTraceParent = $"00-{ctx.TraceId.ToHexString()}-{ctx.SpanId.ToHexString()}-{flags}";
                    }
                    else
                    {
                        // Generate a valid traceparent as last resort
                        var newTraceId = ActivityTraceId.CreateRandom().ToHexString();
                        var newSpanId = ActivitySpanId.CreateRandom().ToHexString();
                        currentTraceParent = $"00-{newTraceId}-{newSpanId}-00";
                    }
                    
                    headers.Add("traceparent", Encoding.UTF8.GetBytes(currentTraceParent));
                    
                    await _resourceAcquiredProducer.ProduceAsync(
                        KafkaTopic.ResourceAcquired.ToString(),
                        new Message<ResourceKey, ResourceAcquired>
                        {
                            Key = new ResourceKey
                            {
                                FacilityId = message.FacilityId,
                                CorrelationId = message.CorrelationId
                            },
                            Headers = headers,
                            Value = message.ResourceAcquired
                        }, cancellationToken);

                    await dataAcquisitionLogManager.UpdateTailFlagForFacilityCorrelationIdReportTrackingId(
                        message.LogIds,
                        message.FacilityId,
                        message.CorrelationId,
                        message.ResourceAcquired.ScheduledReports.FirstOrDefault()?.ReportTrackingId,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "An exception occurred while attempting to send Tail Kafka Messages for facility {facilityId}.",
                        message.FacilityId?.SanitizeUntrustedString());
                }

            }

            _resourceAcquiredProducer.Flush(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Aggregated errors during tailing message processing.");
            throw;
        }
    }

    private ActivityContext CreateActivityContext(string? traceParentId)
    {
        ActivityContext parentContext = default;
        if (!string.IsNullOrEmpty(traceParentId))
        {
            try
            {
                var parts = traceParentId.Split('-');
                if (parts.Length >= 4)
                {
                    var traceId = ActivityTraceId.CreateFromString(parts[1].AsSpan());
                    var parentSpanId = ActivitySpanId.CreateFromString(parts[2].AsSpan());
                    var flags = parts[3] == "01" ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None;
            
                    parentContext = new ActivityContext(traceId, parentSpanId, flags);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse traceparent: {TraceParentId}", traceParentId?.SanitizeUntrustedString());
            }
        }
        return parentContext;
    }
}