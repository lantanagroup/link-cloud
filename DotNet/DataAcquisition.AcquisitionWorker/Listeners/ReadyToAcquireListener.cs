using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.AcquisitionWorker.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Internal;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Utilities;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;

namespace LantanaGroup.Link.DataAcquisition.AcquisitionWorker.Listeners;

public class ReadyToAcquireListener : BaseListener<ReadyToAcquire, long, ReadyToAcquire, string, ResourceAcquired>
{
    ILogger<BaseListener<ReadyToAcquire, long, ReadyToAcquire, string, ResourceAcquired>> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public ReadyToAcquireListener(
        ILogger<ReadyToAcquireListener> logger,
        IKafkaConsumerFactory<long, ReadyToAcquire> kafkaConsumerFactory,
        IDeadLetterExceptionHandler<ReadyToAcquire, long, ReadyToAcquire> deadLetterConsumerHandler,
        IDeadLetterExceptionHandler<ReadyToAcquire, string, string> deadLetterConsumerErrorHandler,
        ITransientExceptionHandler<ReadyToAcquire, long, ReadyToAcquire> transientExceptionHandler,
        ServiceInformation serviceInformation,
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
            GroupId = ServiceInformation.ServiceConfigName,
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
        
        var abortRegistry = scope.ServiceProvider.GetService<IPipelineAbortRegistry>();
        if (abortRegistry != null &&
            await abortRegistry.IsAbortedAsync(value.FacilityId, value.ReportTrackingId, cancellationToken))
        {
            _logger.LogInformation(
                "Skipping ReadyToAcquire for aborted pipeline FacilityId={FacilityId}, ReportTrackingId={ReportTrackingId}, LogId={LogId}.",
                value.FacilityId, value.ReportTrackingId, value.LogId);
            return;
        }

        var logManager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();
        var processor = scope.ServiceProvider.GetRequiredService<AcquisitionProcessorBackgroundService>();

        // ATOMIC STEP: Attempt to "claim" the log - single DB write, no read needed
        var logId = value.LogId.Value;
        bool claimed = await logManager.TrySetLogToQueuedAsync(logId, cancellationToken);

        if (!claimed)
        {
            _logger.LogInformation("LogId {LogId} was already claimed or is in a non-processable state. Skipping duplicate request.", logId);
            return;
        }

        try
        {
            await processor.EnqueueAsync(new AcquisitionWorkItem(
                LogId: logId,
                FacilityId: value.FacilityId,
                IsPerformanceMode: KafkaHeaderHelper.IsPerformanceMode(consumeResult.Message?.Headers)
            ), cancellationToken);
            _logger.LogInformation("Queued LogId {LogId} for facility {FacilityId}", logId, value.FacilityId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue work item for LogId {LogId}. Attempting to revert status.", logId);
            // Revert to Pending so the next scheduled trigger can try again - single atomic write, no read needed
            bool compensationSucceeded = await logManager.TrySetLogStatusAsync(logId,
                new List<RequestStatus> { RequestStatus.Queued }, RequestStatus.Pending, cancellationToken: cancellationToken);

            if (!compensationSucceeded)
            {
                _logger.LogError(ex,
                    "Failed to enqueue work item for LogId {LogId} and compensation status update from Queued to Pending also failed.",
                    logId);
                throw new DeadLetterException($"Compensation failed for LogId {logId} after enqueue failure.", ex);
            }
        }
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