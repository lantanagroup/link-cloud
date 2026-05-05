using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Internal;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.Options;
using System.Text;
using System.Threading.Channels;

namespace LantanaGroup.Link.DataAcquisition.AcquisitionWorker.Services;

public class AcquisitionProcessorBackgroundService : BackgroundService
{
    private readonly ILogger<AcquisitionProcessorBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly Channel<AcquisitionWorkItem> _workChannel;
    private readonly IProducer<ResourceKey, ResourcesAcquired> _resourceAcquiredProducer;

    // Tune these via configuration if desired
    private readonly int _maxConcurrency = 8;          // adjust based on CPU / expected query duration
    private readonly int _channelCapacity = 200;       // backpressure threshold

    public AcquisitionProcessorBackgroundService(
        ILogger<AcquisitionProcessorBackgroundService> logger,
        IServiceProvider serviceProvider,
        IProducer<ResourceKey, ResourcesAcquired> resourceAcquiredProducer,
        IOptions<AcquisitionWorkerProcessorSettings>? settings = null
        )
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _resourceAcquiredProducer = resourceAcquiredProducer;

        if (settings?.Value != null)
        {
            _maxConcurrency = settings.Value.MaxConcurrentAcquisitions;
            _channelCapacity = settings.Value.WorkChannelCapacity;
        }

        _workChannel = Channel.CreateBounded<AcquisitionWorkItem>(
            new BoundedChannelOptions(_channelCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            });
    }

    public virtual async ValueTask EnqueueAsync(AcquisitionWorkItem item, CancellationToken ct = default)
    {
        // Wait up to 5 seconds for space to become available
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            if (await _workChannel.Writer.WaitToWriteAsync(cts.Token))
            {
                await _workChannel.Writer.WriteAsync(item, ct);
                _logger.LogDebug("Enqueued acquisition work for LogId {LogId}", item.LogId);
                return;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Channel full. Timed out enqueuing LogId {LogId}.", item.LogId);
            throw new Exception($"Internal queue capacity reached for LogId {item.LogId}");
        }

        throw new Exception($"Failed to enqueue work item for LogId {item.LogId}");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = _maxConcurrency,
                CancellationToken = stoppingToken
            };

            await Parallel.ForEachAsync(
                _workChannel.Reader.ReadAllAsync(stoppingToken),
                parallelOptions,
                async (item, ct) =>
                {
                    await ProcessWorkItemAsync(item, ct);
                });
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error in acquisition processor background service");
        }
    }

    private async Task ProcessWorkItemAsync(AcquisitionWorkItem item, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var logQueries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();
        var logManager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();
        var patientDataService = scope.ServiceProvider.GetRequiredService<IPatientDataService>();
        var producerFactory = scope.ServiceProvider.GetRequiredService<IKafkaProducerFactory<long, ReadyToAcquire>>();
        var dependencyChecker = scope.ServiceProvider.GetRequiredService<IAcquisitionDependencyChecker>();

        DataAcquisitionLogModel? log = null;

        _logger.LogInformation("Processing acquisition work for LogId {LogId} at FacilityId {FacilityId}", item.LogId, item.FacilityId);

        try
        {
            log = await logQueries.GetAsync(item.LogId, ct);
            if (log == null)
            {
                _logger.LogWarning("Log {LogId} not found during processing - skipping", item.LogId);
                return;
            }

            if (log.Status != RequestStatus.Queued)
            {
                _logger.LogInformation("Log {LogId} no longer in Queued state ({Status}) - skipping",
                    log.Id.ToString().SanitizeForLog(), log.Status?.ToString()?.SanitizeForLog());
                return;
            }

            var depResult = await dependencyChecker.CheckDependenciesAsync(log, ct);
            if (!depResult.AreDependenciesMet)
            {
                var blockingList = string.Join(", ", depResult.BlockingResourceTypes);
                _logger.LogInformation(
                    "Log {LogId} has unmet dependencies ({BlockingTypes}). Deferring to Pending.",
                    log.Id.SanitizeForLog(), blockingList.SanitizeForLog());

                await logManager.TrySetLogStatusAsync(
                    log.Id,
                    [RequestStatus.Queued],
                    RequestStatus.Pending,
                    note: $"[{DateTime.UtcNow:O}] Deferred: waiting for {blockingList} queries to complete.",
                    cancellationToken: ct);

                return;
            }
        }
        catch (OperationCanceledException)
        {
            throw; // Shutdown must propagate
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch/validate LogId {LogId} for facility {FacilityId}. " +
                "The log will be recovered by the stalled-queue housekeeping job.", item.LogId, item.FacilityId);

            try
            {
                if (log != null)
                {
                    var safeMessage = $"[{DateTime.UtcNow:O}] Processing failed: {ex.GetType().Name} - {ex.Message}";
                    log.Status = RequestStatus.Failed;

                    await logManager.UpdateAsync(new UpdateDataAcquisitionLogModel
                    {
                        Id = log.Id,
                        Status = log.Status,
                        NewNotes = [safeMessage],
                        ResourceAcquiredIds = log.ResourceAcquiredIds,
                        RetryAttempts = log.RetryAttempts,
                        CompletionDate = log.CompletionDate,
                        CompletionTimeMilliseconds = log.CompletionTimeMilliseconds,
                        TraceId = log.TraceId,
                        ExecutionDate = log.ExecutionDate
                    }, ct);
                }
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx,
                    "Additionally failed to update status for LogId {LogId}. " +
                    "The log will be recovered by the stalled-queue housekeeping job.", item.LogId);
            }

            // Do NOT re-throw a transient DB error for one work item must not
            // kill the Parallel.ForEachAsync loop and take down the entire
            // background service. The log stays in Queued state and will be
            // recovered by FailStalledQueuedLogsAsync on the next job cycle.
            return;
        }

        try
        {
            await patientDataService.ExecuteLogRequest(
                new AcquisitionRequest(log.Id, item.FacilityId),
                ct);

            _logger.LogInformation("Successfully completed acquisition for LogId {LogId}", log.Id);

            // Inline tail check if all siblings are terminal, produce AcquisitionComplete.
            await TryProduceTailMessageAsync(scope.ServiceProvider, logManager, log.Id, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process LogId {LogId} for facility {FacilityId}", item.LogId, item.FacilityId);

            // Even on failure the log may now be in a terminal status (MaxRetriesReached),
            // so attempt the tail check to avoid stalling downstream.
            try
            {
                await TryProduceTailMessageAsync(scope.ServiceProvider, logManager, log.Id, ct);
            }
            catch (Exception tailEx)
            {
                _logger.LogWarning(tailEx, "Post-failure tail check also failed for LogId {LogId}. Safety-net poller will recover.", item.LogId);
            }
        }
    }

    private async Task TryProduceTailMessageAsync(IServiceProvider scopeProvider, IDataAcquisitionLogManager logManager, long logId, CancellationToken ct)
    {
        try
        {
            var tailResult = await logManager.TryCompleteTailAsync(logId, ct);
            if (tailResult == null)
            {
                return; // Group not yet complete.
            }

            var headers = new Headers
            {
                new Header(DataAcquisitionConstants.HeaderNames.CorrelationId,
                    Encoding.UTF8.GetBytes(tailResult.CorrelationId))
            };

            if (!string.IsNullOrEmpty(tailResult.TraceParentId))
            {
                headers.Add("traceparent", Encoding.UTF8.GetBytes(tailResult.TraceParentId));
            }


            await _resourceAcquiredProducer.ProduceAsync(
                    KafkaTopic.ResourcesAcquired.ToString(),
                    new Message<ResourceKey, ResourcesAcquired>
                    {
                        Key = new ResourceKey
                        {
                            FacilityId = tailResult.FacilityId,
                            PatientId = tailResult.PatientId
                        },
                        Headers = headers,
                        Value = tailResult.ResourcesAcquired
                    },
                    ct);

            _logger.LogInformation(
                "Produced inline AcquisitionComplete tail for FacilityId={FacilityId}, CorrelationId={CorrelationId}",
                tailResult.FacilityId.SanitizeForLog(), tailResult.CorrelationId.SanitizeForLog());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to produce inline tail message for LogId {LogId}. Safety-net poller will recover.", logId);
        }
    }
}