using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using Microsoft.Extensions.Options;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

namespace LantanaGroup.Link.DataAcquisition.AcquisitionWorker.Listeners;

public class ReadyToAcquireListener : BaseListener<ReadyToAcquire, string, ReadyToAcquire, string, ResourceAcquired>
{
    ILogger<BaseListener<ReadyToAcquire, string, ReadyToAcquire, string, ResourceAcquired>> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public ReadyToAcquireListener(
        ILogger<ReadyToAcquireListener> logger,
        IKafkaConsumerFactory<string, ReadyToAcquire> kafkaConsumerFactory,
        IDeadLetterExceptionHandler<string, ReadyToAcquire> deadLetterConsumerHandler,
        IDeadLetterExceptionHandler<string, string> deadLetterConsumerErrorHandler,
        ITransientExceptionHandler<string, ReadyToAcquire> transientExceptionHandler,
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

    protected override async Task ExecuteListenerAsync(ConsumeResult<string, ReadyToAcquire> consumeResult, CancellationToken cancellationToken = default)
    {
        var logId = consumeResult.Message?.Value?.LogId;
        var facilityId = consumeResult.Message?.Value?.FacilityId;

        if (string.IsNullOrWhiteSpace(logId) || string.IsNullOrWhiteSpace(facilityId))
        {
            _logger.LogError("LogId or FacilityId is null or empty in ReadyToAcquire message. LogId: {LogId}, FacilityId: {FacilityId}", logId, facilityId);
            throw new DeadLetterException("LogId or FacilityId is null or empty in ReadyToAcquire message.");
        }

        _logger.LogInformation("Processing ReadyToAcquire message with log id: {consumeResult.Message.Value.LogId}, and facility id: {consumeResult.Message.Value.FacilityId}", consumeResult.Message.Value.LogId, consumeResult.Message.Value.FacilityId);

        try
        {
            var scope = _serviceScopeFactory.CreateScope();
            var patientDataService = scope.ServiceProvider.GetRequiredService<IPatientDataService>();

            // Process the ReadyToAcquire message
            await patientDataService.ExecuteLogRequest(new AcquisitionRequest(logId, facilityId), cancellationToken);
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Error processing ReadyToAcquire message with log id: {consumeResult.Message.Value.LogId}, and facility id: {consumeResult.Message.Value.FacilityId}", consumeResult.Message.Value.LogId, consumeResult.Message.Value.FacilityId);
            throw new DeadLetterException("Error processing ReadyToAcquire message", ex);
        }
    }

    protected override string ExtractCorrelationId(ConsumeResult<string, ReadyToAcquire> consumeResult)
    {
        return "";
    }

    protected override string ExtractFacilityId(ConsumeResult<string, ReadyToAcquire> consumeResult)
    {
        if (string.IsNullOrWhiteSpace(consumeResult.Message.Value.FacilityId)) return null;
        return consumeResult.Message.Value.FacilityId;
    }
}
