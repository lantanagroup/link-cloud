using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.AcquisitionWorker.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Internal;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace LantanaGroup.Link.DataAcquisition.AcquisitionWorker.Listeners;

public class ReadyToAcquireListener : BaseListener<ReadyToAcquire, long, ReadyToAcquire, string, ResourceAcquired>
{
    ILogger<BaseListener<ReadyToAcquire, long, ReadyToAcquire, string, ResourceAcquired>> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public ReadyToAcquireListener(
        ILogger<ReadyToAcquireListener> logger,
        IKafkaConsumerFactory<long, ReadyToAcquire> kafkaConsumerFactory,
        IDeadLetterExceptionHandler<long, ReadyToAcquire> deadLetterConsumerHandler,
        IDeadLetterExceptionHandler<string, string> deadLetterConsumerErrorHandler,
        ITransientExceptionHandler<long, ReadyToAcquire> transientExceptionHandler,
        IOptions<ServiceInformation> serviceInformation,
        IServiceScopeFactory serviceScopeFactory)
        : base(logger, kafkaConsumerFactory, deadLetterConsumerHandler, deadLetterConsumerErrorHandler, transientExceptionHandler, serviceInformation)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override ConsumerConfig CreateConsumerConfig()
    {
        var settings = new ConsumerConfig
        {
            EnableAutoCommit = false,
            GroupId = ServiceActivitySource.ServiceName
        };
        return settings;
    }

    protected override async Task ExecuteListenerAsync(ConsumeResult<long, ReadyToAcquire> consumeResult, CancellationToken cancellationToken = default)
    {
        var value = consumeResult.Message?.Value;
        if (value?.LogId == null || string.IsNullOrWhiteSpace(value.FacilityId))
        {
            _logger.LogError("Invalid ReadyToAcquire message - missing LogId or FacilityId");
            throw new DeadLetterException("Invalid ReadyToAcquire message");
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var logQueries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();
        var logManager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();
        var processor = scope.ServiceProvider.GetRequiredService<AcquisitionProcessorBackgroundService>();

        // ATOMIC STEP: Attempt to "claim" the log
        // This replaces the GetAsync -> Check Status -> UpdateAsync flow
        var logId = value.LogId.Value;
        bool claimed = await logQueries.TrySetLogToQueuedAsync(logId, cancellationToken);

        if (!claimed)
        {
            _logger.LogInformation("LogId {LogId} was already claimed or is in a non-processable state. Skipping duplicate request.", logId);
            return;
        }

        // getting the log in case the enqueue fails and we need to revert the status
        var log = await logQueries.GetAsync(logId, cancellationToken);
        log.Notes ??= new List<string>();
        log.Notes.Add($"[{DateTime.UtcNow:O}] Queued for background acquisition processing");
        await logManager.UpdateAsync(new UpdateDataAcquisitionLogModel
        {
            Id = log.Id,
            Notes = log.Notes,
            ResourceAcquiredIds = log.ResourceAcquiredIds,
            RetryAttempts = log.RetryAttempts,
            CompletionDate = log.CompletionDate,
            CompletionTimeMilliseconds = log.CompletionTimeMilliseconds,
            ExecutionDate = log.ExecutionDate,
            Status = log.Status,
            TraceId = log.TraceId
        });

        try
        {
            await processor.EnqueueAsync(new AcquisitionWorkItem(
                LogId: logId,
                FacilityId: value.FacilityId
            ), cancellationToken);
            _logger.LogInformation("Queued LogId {LogId} for facility {FacilityId}", logId, value.FacilityId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue work item for LogId {LogId}. Attempting to revert status.", logId);
            // Minimally invasive: set back to Pending so the next trigger can try again
            log.Status = RequestStatus.Pending;
            log.Notes.Add($"[{DateTime.UtcNow:O}] Enqueue failed, reverting to Pending.");
            await logManager.UpdateAsync(new UpdateDataAcquisitionLogModel { Id = log.Id, Status = log.Status, Notes = log.Notes });
            throw new DeadLetterException("Failed to enqueue work item", ex); // Re-throw to let Kafka handle the retry/DLQ logic
        }
        // Method ends → offset committed quickly by base class
    }

    protected override string ExtractCorrelationId(ConsumeResult<long, ReadyToAcquire> consumeResult)
    {
        return "";
    }

    protected override string ExtractFacilityId(ConsumeResult<long, ReadyToAcquire> consumeResult)
    {
        if (string.IsNullOrWhiteSpace(consumeResult.Message.Value.FacilityId)) return null;
        return consumeResult.Message.Value.FacilityId;
    }
}