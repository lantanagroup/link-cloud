using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Internal;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Interfaces;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace LantanaGroup.Link.DataAcquisition.AcquisitionWorker.Services;

public class AcquisitionProcessorBackgroundService : BackgroundService
{
    private readonly ILogger<AcquisitionProcessorBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly Channel<AcquisitionWorkItem> _workChannel;

    // Tune these via configuration if desired
    private readonly int _maxConcurrency = 8;          // adjust based on CPU / expected query duration
    private readonly int _channelCapacity = 200;       // backpressure threshold

    public AcquisitionProcessorBackgroundService(
        ILogger<AcquisitionProcessorBackgroundService> logger,
        IServiceProvider serviceProvider,
        IOptions<AcquisitionWorkerProcessorSettings>? settings = null
        )
    {
        _logger = logger;
        _serviceProvider = serviceProvider;

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

    public async ValueTask EnqueueAsync(AcquisitionWorkItem item, CancellationToken ct = default)
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
        catch (OperationCanceledException ex)
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

        DataAcquisitionLogModel? log = null;

        //debug line
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
                _logger.LogInformation("Log {LogId} no longer in Queued state ({Status}) - skipping", log.Id, log.Status);
                return;
            }

            // Core business logic - unchanged
            await patientDataService.ExecuteLogRequest(
                new AcquisitionRequest(log.Id, item.FacilityId),
                ct);

            _logger.LogInformation("Successfully completed acquisition for LogId {LogId}", log.Id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to process LogId {LogId} for facility {FacilityId}", item.LogId, item.FacilityId);

            if (log != null)
            {
                log.Notes ??= new List<string>();
                var safeMessage = $"[{DateTime.UtcNow:O}] Processing failed: {ex.GetType().Name} - {ex.Message}";
                log.Notes.Add(safeMessage);
                log.Status = RequestStatus.Failed;

                await logManager.UpdateAsync(new UpdateDataAcquisitionLogModel
                {
                    Id = log.Id,
                    Status = log.Status,
                    Notes = log.Notes,
                    ResourceAcquiredIds = log.ResourceAcquiredIds,
                    RetryAttempts = log.RetryAttempts,
                    CompletionDate = log.CompletionDate,
                    CompletionTimeMilliseconds = log.CompletionTimeMilliseconds,
                    TraceId = log.TraceId,
                    ExecutionDate = log.ExecutionDate
                }, ct);
            }
        }
    }
}