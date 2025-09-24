using Confluent.Kafka;
using DataAcquisition.Domain.Settings;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.Options;
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
    private readonly GeneralSettings _generalSettings;

    public AcquisitionProcessingJob(
        ILogger<AcquisitionProcessingJob> logger,
        IServiceScopeFactory serviceScopeFactory,
        IProducer<string, ReadyToAcquire> readyToAcquireProducer,
        ITransientExceptionHandler<string, ReadyToAcquire> transientExceptionHandler,
        IProducer<string, ResourceAcquired> resourceAcquiredProducer,
        IOptions<GeneralSettings> generalSettings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _readyToAcquireProducer = readyToAcquireProducer ?? throw new ArgumentNullException(nameof(readyToAcquireProducer));
        _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentNullException(nameof(transientExceptionHandler));
        _resourceAcquiredProducer = resourceAcquiredProducer ?? throw new ArgumentNullException(nameof(resourceAcquiredProducer));
        _generalSettings = generalSettings?.Value ?? throw new ArgumentNullException(nameof(generalSettings));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        await ProcessPendingLogs();

        await ProcessPendingTailingMessages();
    }

    private async Task ProcessPendingLogs()
    {
        string? facilityId = string.Empty;
        ReadyToAcquire messageValue = null;
        try
        {
            //set scope for DataAcquisitionLogManager
            using var scope = _serviceScopeFactory.CreateScope();
            var _dataAcquisitionLogManager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();
            var _fhirQueryConfigurationManager = scope.ServiceProvider.GetRequiredService<IFhirQueryConfigurationManager>();

            //get pending requests
            var pendingRequests = await _dataAcquisitionLogManager.GetPendingRequests();

            _logger.BeginScope("Processing {count} pending requests", pendingRequests.Count);

            //process each request
            foreach (var request in pendingRequests)
            {
                if (request.Notes == null)
                {
                    request.Notes = new List<string>();
                }

                var config = await _fhirQueryConfigurationManager.GetAsync(request.FacilityId);

                if (config == null)
                {
                    //update log record to FAILED and note that configuration for the
                    //specified facility is not present.
                    var baseMessage = "Request FAILED due to missing FhirQueryConfiguration. FacilityId:";
                    request.Status = RequestStatus.Failed;
                    request.Notes.Add($"{baseMessage} {request.FacilityId}.");
                    _logger.LogCritical("{baseMessage} {faciltyId}, RequestId: {id}", baseMessage.Sanitize(), request.FacilityId.Sanitize(), request.Id.Sanitize());
                    await _dataAcquisitionLogManager.UpdateAsync(request);

                    continue;
                }

                //check if retryCount x retryInterval has been exceeded using ModifyDate
                //if so, skip processing this request
                var retryAttempts = request.RetryAttempts ?? 0;
                if (retryAttempts > 0 && request.ModifyDate.HasValue)
                {
                    var nextAttemptTime = request.ModifyDate.Value.AddMinutes((double)(retryAttempts * _generalSettings.RetryDelayMinutes));
                    if (DateTime.UtcNow < nextAttemptTime)
                    {
                        _logger.LogInformation("Skipping request with log id: {logId} for facility id: {facilityId}. Next attempt time is at or after {nextAttemptTime}.", request.Id.Sanitize(), request.FacilityId.Sanitize(), nextAttemptTime);
                        continue;
                    }
                }

                var currentTime = DateTime.UtcNow.TimeOfDay;
                if (((config.MinAcquisitionPullTime == null || config.MinAcquisitionPullTime == default) && 
                    (config.MaxAcquisitionPullTime == null || config.MaxAcquisitionPullTime == default)) ||
                    (currentTime >= config.MinAcquisitionPullTime && currentTime <= config.MaxAcquisitionPullTime))
                {
                    //set facility id
                    facilityId = request.FacilityId;
                    messageValue = new ReadyToAcquire { FacilityId = facilityId, LogId = request.Id };

                    //process request
                    _logger.LogInformation("Generating ReadyToAcquire message for log id: {request.Id}", request.Id.Sanitize());

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
                                                FacilityId = request.FacilityId                                  
                                            },
                                            Headers = headers
                                        });
                        _readyToAcquireProducer.Flush();

                        await _dataAcquisitionLogManager.UpdateLogStatusAsync(request.Id, RequestStatus.Ready);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error producing ReadyToAcquire message for log id: {logId}", request.Id.Sanitize());

                        //ensure that log remains in "Pending" state.
                        request.Status = RequestStatus.Failed;
                        await _dataAcquisitionLogManager.UpdateAsync(request);
                    }

                    facilityId = string.Empty;
                    messageValue = null; 
                }
                else
                {
                    _logger.LogInformation("Current time {currentTime} is outside the acquisition window for facility id: {facilityId}. Acquisition window is {minTime} to {maxTime}.", currentTime, request.FacilityId.Sanitize(), config.MinAcquisitionPullTime, config.MaxAcquisitionPullTime);
                    //update log notes to indicate that request was not processed due to acquisition window
                    request.Notes.Add($"Request not processed. Current time {currentTime} is outside the acquisition window for facility id: {request.FacilityId}. Acquisition window is {config.MinAcquisitionPullTime} to {config.MaxAcquisitionPullTime}.");
                    await _dataAcquisitionLogManager.UpdateAsync(request);
                }
            }
            _logger.LogInformation("Completed processing pending requests.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing acquisition job for facility id: {facilityId}", facilityId);
            
        }
    }

    private async Task ProcessPendingTailingMessages()
    {
        //set scope for DataAcquisitionLogManager
        using var scope = _serviceScopeFactory.CreateScope();
        var _dataAcquisitionLogManager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();
        var _dataAcquisitionLogQueries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();  
        
        IEnumerable<TailingMessageModel> tailingMessages = null;
        try
        {
            tailingMessages = await _dataAcquisitionLogQueries.GetTailingMessages();
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
                            });
                    _resourceAcquiredProducer.Flush();

                    await _dataAcquisitionLogManager.UpdateTailFlagForFacilityCorrelationIdReportTrackingId(
                        message.LogIds,
                        message.Key,
                        message.CorrelationId,
                        message.ResourceAcquired.ScheduledReports.FirstOrDefault()?.ReportTrackingId,
                        CancellationToken.None);
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
