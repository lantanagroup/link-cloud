using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Quartz;
using System.Text;
using RequestStatus = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.DataAcquisition.Jobs;

[DisallowConcurrentExecution]
public class AcquisitionProcessingJob : IJob
{
    private readonly ILogger<AcquisitionProcessingJob> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IProducer<string, ReadyToAcquire> _readyToAcquireProducer;
    protected readonly ITransientExceptionHandler<string, ReadyToAcquire> _transientExceptionHandler;
    private readonly IProducer<string, ResourceAcquired> _resourceAcquiredProducer;

    public AcquisitionProcessingJob(
        ILogger<AcquisitionProcessingJob> logger,
        IServiceScopeFactory serviceScopeFactory,
        IProducer<string, ReadyToAcquire> readyToAcquireProducer,
        ITransientExceptionHandler<string, ReadyToAcquire> transientExceptionHandler,
        IProducer<string, ResourceAcquired> resourceAcquiredProducer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _readyToAcquireProducer = readyToAcquireProducer ?? throw new ArgumentNullException(nameof(readyToAcquireProducer));
        _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentNullException(nameof(transientExceptionHandler));
        _resourceAcquiredProducer = resourceAcquiredProducer ?? throw new ArgumentNullException(nameof(resourceAcquiredProducer));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        await ProcessPendingLogs(context.CancellationToken);

        await ProcessPendingTailingMessages(context.CancellationToken);
    }

    private async Task ProcessPendingLogs(CancellationToken cancellationToken)
    {
        string? facilityId = string.Empty;
        ReadyToAcquire messageValue = null;
        try
        {
            //set scope for DataAcquisitionLogManager
            using var scope = _serviceScopeFactory.CreateScope();
            var _dataAcquisitionLogManager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();
            var _fhirQueryConfigurationManager = scope.ServiceProvider.GetRequiredService<IFhirQueryConfigurationManager>();
            var _dataAcquisitionLogQueries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

            //get pending and retryable (Failed) Logs
            var processableRequests = await _dataAcquisitionLogQueries.GetPendingAndRetryableFailedRequests(cancellationToken);

            _logger.BeginScope("Processing {count} processable requests", processableRequests.Count);

            //process each request
            foreach (var request in processableRequests)
            {
                request.RetryAttempts ??= 0;
                request.Notes ??= new List<string>();

                var config = await _fhirQueryConfigurationManager.GetAsync(request.FacilityId, cancellationToken);

                if (config == null)
                {
                    //update log record to FAILED and note that configuration for the specified facility is not present.
                    var baseMessage = "Request FAILED due to missing FhirQueryConfiguration. FacilityId:";
                    request.Status = RequestStatus.Failed;
                    request.Notes.Add($"{baseMessage} {request.FacilityId}.");
                    _logger.LogCritical("{baseMessage} {facilityId}, RequestId: {id}", baseMessage.Sanitize(), request.FacilityId.Sanitize(), request.Id.Sanitize());
                    await _dataAcquisitionLogManager.UpdateAsync(request, cancellationToken);
                    continue;
                }

                var currentTime = DateTime.UtcNow.TimeOfDay;
                if ((config.MinAcquisitionPullTime == default && config.MaxAcquisitionPullTime == default) ||
                    (currentTime >= config.MinAcquisitionPullTime && currentTime <= config.MaxAcquisitionPullTime))
                {

                    if (request.Status == RequestStatus.Failed)
                    {
                        if (request.RetryAttempts == 10)
                        {
                            request.Status = RequestStatus.MaxRetriesReached;
                            request.Notes.Add($"[{DateTime.UtcNow}] Maximum retry attempts (10) reached for request.");
                            await _dataAcquisitionLogManager.UpdateAsync(request, cancellationToken);
                            continue;
                        }

                        request.RetryAttempts += 1;
                        request.Notes.Add($"[{DateTime.UtcNow}] Retrying failed request. Attempt {request.RetryAttempts}.");
                    }

                    //set facility id
                    facilityId = request.FacilityId;
                    messageValue = new ReadyToAcquire { FacilityId = facilityId, LogId = request.Id };

                    _logger.LogInformation("Generating ReadyToAcquire message for log id: {request.Id}", request.Id.Sanitize());

                    // Update status and other fields in a single transaction
                    request.Status = RequestStatus.Ready;
                    await _dataAcquisitionLogManager.UpdateAsync(request, cancellationToken);

                    try
                    {
                        _logger.LogInformation("Producing ReadyToAcquire message for log id: {logId} and facility id: {facilityId}", request.Id.Sanitize(), request.FacilityId.Sanitize());

                        var headers = new Headers
                        {
                            { "X-Correlation-Id", Encoding.UTF8.GetBytes(request.CorrelationId?.ToString() ?? string.Empty) }
                        };

                        await _readyToAcquireProducer.ProduceAsync(
                            KafkaTopic.ReadyToAcquire.ToString(),
                            new Message<string, ReadyToAcquire>
                            {
                                Key = request.Id,
                                Value = new ReadyToAcquire
                                {
                                    LogId = request.Id,
                                    FacilityId = request.FacilityId,
                                    ReportTrackingId = request.ReportTrackingId
                                },
                                Headers = headers
                            }, cancellationToken);
                        _readyToAcquireProducer.Flush(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error producing ReadyToAcquire message for log id: {logId}", request.Id.Sanitize());

                        //ensure that log remains in "Failed" state.
                        request.Status = RequestStatus.Failed;
                        request.Notes.Add($"[{DateTime.UtcNow}] Failed to produce ReadyToAcquire message: {ex.Message}");
                        await _dataAcquisitionLogManager.UpdateAsync(request, cancellationToken);
                    }

                    facilityId = string.Empty;
                    messageValue = null;
                }
            }
            _logger.LogInformation("Completed processing processable requests.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing acquisition job for facility id: {facilityId}", facilityId);
            _transientExceptionHandler.HandleException(ex, messageValue, facilityId, $"Error processing acquisition job for facility id: {facilityId}");
        }
    }

    private async Task ProcessPendingTailingMessages(CancellationToken cancellationToken)
    {
        //set scope for DataAcquisitionLogManager
        using var scope = _serviceScopeFactory.CreateScope();
        var _dataAcquisitionLogManager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();
        var _dataAcquisitionLogQueries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        IEnumerable<TailingMessageModel> tailingMessages = null;
        try
        {
            tailingMessages = await _dataAcquisitionLogQueries.GetTailingMessages(cancellationToken);
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
                    await _resourceAcquiredProducer.ProduceAsync(
                        KafkaTopic.ResourceAcquired.ToString(),
                        new Message<string, ResourceAcquired>
                        {
                            Key = message.Key,
                            Headers = new Headers
                            {
                                new Header(DataAcquisitionConstants.HeaderNames.CorrelationId, Encoding.UTF8.GetBytes(message.CorrelationId))
                            },
                            Value = message.ResourceAcquired
                        }, cancellationToken);
                    _readyToAcquireProducer.Flush(cancellationToken);

                    await _dataAcquisitionLogManager.UpdateTailFlagForFacilityCorrelationIdReportTrackingId(
                        message.LogIds,
                        message.Key,
                        message.CorrelationId,
                        message.ResourceAcquired.ScheduledReports.FirstOrDefault()?.ReportTrackingId,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An exception occurred while attempting to send Tail Kafka Messages.");
                    throw;
                }
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}