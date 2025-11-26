using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Quartz;
using System.Diagnostics;
using System.Text;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using RequestStatus = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;
using Task = System.Threading.Tasks.Task;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;

namespace LantanaGroup.Link.DataAcquisition.Jobs;

[DisallowConcurrentExecution]
public class AcquisitionProcessingJob : IJob
{
    private readonly ILogger<AcquisitionProcessingJob> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IProducer<long, ReadyToAcquire> _readyToAcquireProducer;
    private readonly IProducer<string, ResourceAcquired> _resourceAcquiredProducer;
    private const int BatchSize = 25;
    private const int MaxRetryAttempts = 10;
    private const int MaxConcurrency = 8;

    public AcquisitionProcessingJob(
        ILogger<AcquisitionProcessingJob> logger,
        IServiceScopeFactory serviceScopeFactory,
        IProducer<long, ReadyToAcquire> readyToAcquireProducer,
        IProducer<string, ResourceAcquired> resourceAcquiredProducer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _readyToAcquireProducer = readyToAcquireProducer ?? throw new ArgumentNullException(nameof(readyToAcquireProducer));
        _resourceAcquiredProducer = resourceAcquiredProducer ?? throw new ArgumentNullException(nameof(resourceAcquiredProducer));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        await ProcessPendingLogs(context.CancellationToken);
        await ProcessPendingTailingMessages(context.CancellationToken);
    }

    public async Task ProcessPendingLogs(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dataAcquisitionLogQueries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();
            var facilities = await dataAcquisitionLogQueries.GetFacilitiesWithPendingAndRetryableFailedRequests(cancellationToken);

            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrency, CancellationToken = cancellationToken };

            await Parallel.ForEachAsync(facilities, parallelOptions, async (facilityId, ct) =>
            {
                await ProcessFacilityPendingLogs(facilityId, cancellationToken);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving facilities for processing pending logs.");
        }
    }

    private async Task ProcessFacilityPendingLogs(string facilityId, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dataAcquisitionLogManager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();
            var fhirQueryConfigurationQueries = scope.ServiceProvider.GetRequiredService<IFhirQueryConfigurationQueries>();
            var dataAcquisitionLogQueries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

            var config = await fhirQueryConfigurationQueries.GetByFacilityIdAsync(facilityId, cancellationToken);

            if (config == null)
            {
                _logger.LogCritical("Request FAILED due to missing FhirQueryConfiguration. FacilityId: {facilityId}", facilityId.Sanitize());

                long? lastMissingConfigId = null;
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var requests = await dataAcquisitionLogQueries.GetNextEligibleBatchForFacility(facilityId, lastMissingConfigId, BatchSize, cancellationToken);
                    if (!requests.Any()) break;

                    foreach (var log in requests)
                    {
                        log.Status = RequestStatus.Failed;
                        log.Notes ??= new List<string>();
                        log.Notes.Add($"[{DateTime.UtcNow}] Request FAILED due to missing FhirQueryConfiguration. FacilityId: {log.FacilityId}.");
                        await dataAcquisitionLogManager.UpdateAsync(new UpdateDataAcquisitionLogModel
                        {
                            Id = log.Id,
                            ResourceAcquiredIds = log.ResourceAcquiredIds,
                            RetryAttempts = log.RetryAttempts,
                            CompletionDate = log.CompletionDate,
                            CompletionTimeMilliseconds = log.CompletionTimeMilliseconds, 
                            TraceId = log.TraceId,
                            ExecutionDate = log.ExecutionDate,
                            Notes = log.Notes,
                            Status = log.Status,
                        }, cancellationToken);
                    }

                    lastMissingConfigId = requests.Last().Id;
                }

                return;
            }

            if (!IsWithinAcquisitionWindow(config.MinAcquisitionPullTime, config.MaxAcquisitionPullTime))
            {
                _logger.LogInformation("Current time {currentTime} is outside the acquisition window for facility {facilityId}.", DateTime.UtcNow.TimeOfDay, facilityId);
                return;
            }

            long? lastId = null;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger.LogInformation("Fetching batch after Id {lastId} for facility {facilityId}", lastId?.ToString() ?? "null", facilityId);
                var requests = await dataAcquisitionLogQueries.GetNextEligibleBatchForFacility(facilityId, lastId, BatchSize, cancellationToken);
                if (!requests.Any())
                {
                    _logger.LogInformation("No more logs to process for facility {facilityId}", facilityId);
                    break;
                }

                _logger.BeginScope("Processing {count} processable requests for facility {facilityId}", requests.Count, facilityId);

                // Serialize processing to avoid DbContext concurrency issues (original was Parallel.ForEachAsync)
                foreach (var log in requests)
                {
                    log.Notes ??= new();
                    if (log.Status == RequestStatus.Failed)
                    {
                        if (log.RetryAttempts >= MaxRetryAttempts)
                        {
                            log.Status = RequestStatus.MaxRetriesReached;
                            log.Notes ??= new List<string>();
                            log.Notes.Add($"[{DateTime.UtcNow}] Maximum retry attempts ({MaxRetryAttempts}) reached for request.");
                            await dataAcquisitionLogManager.UpdateAsync(new UpdateDataAcquisitionLogModel
                            {
                                Id = log.Id,
                                ResourceAcquiredIds = log.ResourceAcquiredIds,
                                RetryAttempts = log.RetryAttempts,
                                CompletionDate = log.CompletionDate,
                                CompletionTimeMilliseconds = log.CompletionTimeMilliseconds, TraceId = log.TraceId,
                                ExecutionDate = log.ExecutionDate,
                                Notes = log.Notes,
                                Status = log.Status,
                            }, cancellationToken);
                            continue;
                        }

                        log.RetryAttempts += 1;
                        log.Notes.Add($"[{DateTime.UtcNow}] Retrying failed request. Attempt {log.RetryAttempts}.");
                    }

                    var messageValue = new ReadyToAcquire { FacilityId = facilityId, LogId = log.Id };

                    _logger.LogInformation("Generating ReadyToAcquire message for log id: {requestId}", log.Id);

                    log.Status = RequestStatus.Ready;
                    await dataAcquisitionLogManager.UpdateAsync(new UpdateDataAcquisitionLogModel
                    {
                        Id = log.Id,
                        ResourceAcquiredIds = log.ResourceAcquiredIds,
                        RetryAttempts = log.RetryAttempts,
                        CompletionDate = log.CompletionDate,
                        CompletionTimeMilliseconds = log.CompletionTimeMilliseconds, TraceId = log.TraceId,
                        ExecutionDate = log.ExecutionDate,
                        Notes = log.Notes,
                        Status = log.Status,
                    }, cancellationToken);

                    try
                    {
                        _logger.LogInformation("Producing ReadyToAcquire message for log id: {logId} and facility id: {facilityId}", log.Id, facilityId.Sanitize());

                        var headers = new Headers
                        {
                            { "X-Correlation-Id", Encoding.UTF8.GetBytes(log.CorrelationId?.ToString() ?? string.Empty) }
                        };

                        await _readyToAcquireProducer.ProduceAsync(
                            KafkaTopic.ReadyToAcquire.ToString(),
                            new Message<long, ReadyToAcquire>
                            {
                                Key = log.Id,
                                Value = new ReadyToAcquire
                                {
                                    LogId = log.Id,
                                    FacilityId = facilityId,
                                    ReportTrackingId = log.ReportTrackingId
                                },
                                Headers = headers
                            }, cancellationToken);
                        _readyToAcquireProducer.Flush(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error producing ReadyToAcquire message for log id: {logId}", log.Id);

                        log.Notes ??= new();

                        log.Status = RequestStatus.Failed;
                        log.Notes.Add($"[{DateTime.UtcNow}] Failed to produce ReadyToAcquire message: {ex.Message}");
                        await dataAcquisitionLogManager.UpdateAsync(new UpdateDataAcquisitionLogModel
                        {
                            Id = log.Id,
                            ResourceAcquiredIds = log.ResourceAcquiredIds,
                            RetryAttempts = log.RetryAttempts,
                            CompletionDate = log.CompletionDate,
                            CompletionTimeMilliseconds = log.CompletionTimeMilliseconds, TraceId = log.TraceId,
                            ExecutionDate = log.ExecutionDate,
                            Notes = log.Notes,
                            Status = log.Status,
                        }, cancellationToken);
                    }
                }

                lastId = requests.Last().Id;
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
                        new Message<string, ResourceAcquired>
                        {
                            Key = message.FacilityId,
                            Headers = headers,
                            Value = message.ResourceAcquired
                        }, cancellationToken);

                    _resourceAcquiredProducer.Flush(cancellationToken);

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
                        message.FacilityId);
                }

            }
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
                _logger.LogWarning(ex, "Failed to parse traceparent: {TraceParentId}", traceParentId);
            }
        }
        return parentContext;
    }
}