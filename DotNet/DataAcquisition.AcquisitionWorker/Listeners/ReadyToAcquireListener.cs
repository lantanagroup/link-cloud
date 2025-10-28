using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
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
        var logId = consumeResult.Message?.Value?.LogId;
        var facilityId = consumeResult.Message?.Value?.FacilityId;

        if (logId == default || string.IsNullOrWhiteSpace(facilityId))
        {
            _logger.LogError("LogId or FacilityId is null or empty in ReadyToAcquire message. LogId: {LogId}, FacilityId: {FacilityId}", logId, facilityId);
            throw new DeadLetterException("LogId or FacilityId is null or empty in ReadyToAcquire message.");
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var patientDataService = scope.ServiceProvider.GetRequiredService<IPatientDataService>();
        var logQueries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();
        var dataAcquisitionLogManager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();
        DataAcquisitionLogModel? log = null; 
        _logger.LogInformation("Processing ReadyToAcquire message with log id: {consumeResult.Message.Value.LogId}, and facility id: {consumeResult.Message.Value.FacilityId}", consumeResult.Message.Value.LogId, consumeResult.Message.Value.FacilityId);

        try
        {
            log = await logQueries.GetAsync(logId!.Value, cancellationToken);

            if(log == null)
            {
                throw new DeadLetterException($"No DataAcquisitionLog found for log id: {logId}");
            }

            // Process the ReadyToAcquire message
            await patientDataService.ExecuteLogRequest(new AcquisitionRequest(log.Id, facilityId), cancellationToken);
        }
        catch(ProduceException<string, ResourceAcquired> ex)
        {
            _logger.LogError(ex, "Error producing ReadyToAcquire message for log id: {logId}, facility id: {facilityId}", logId, facilityId);
            throw new TransientException("Error producing ReadyToAcquire message", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PatientDataService.ExecuteLogRequest: [{Time}] Error encountered", DateTime.UtcNow);

            if (log != null)
            {
                log.Notes ??= new();

                log.Status = RequestStatus.Failed;
                log.Notes.Add($"ReadyToAcquireListener.ExecuteListenerAsync: [{DateTime.UtcNow}] Error encountered: {facilityId.Sanitize() ?? string.Empty}\n{ex.Message}\n{ex.InnerException?.Message ?? string.Empty}");
                await dataAcquisitionLogManager.UpdateAsync(new UpdateDataAcquisitionLogModel
                {
                    Id = log.Id,
                    RetryAttempts = log.RetryAttempts,
                    CompletionDate = log.CompletionDate,
                    CompletionTimeMilliseconds = log.CompletionTimeMilliseconds, TraceId = log.TraceId,
                    ExecutionDate = log.ExecutionDate,
                    Notes = log.Notes,
                    Status = log.Status,
                }, cancellationToken);
            }

            _logger.LogError(ex, "Error processing ReadyToAcquire message with log id: {consumeResult.Message.Value.LogId}, and facility id: {consumeResult.Message.Value.FacilityId}", consumeResult.Message.Value.LogId, consumeResult.Message.Value.FacilityId);
            throw new DeadLetterException("Error processing ReadyToAcquire message: " + ex.Message, ex);
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
