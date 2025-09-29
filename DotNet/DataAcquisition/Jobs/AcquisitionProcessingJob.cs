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
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dataAcquisitionLogQueries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();
            var facilities = await dataAcquisitionLogQueries.GetFacilitiesWithPendingAndRetryableFailedRequests(cancellationToken);
            var tasks = facilities.Select(facilityId => ProcessFacilityPendingLogs(facilityId, cancellationToken)).ToArray();
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving facilities for processing pending logs.");
        }
    }

    private async Task ProcessFacilityPendingLogs(string facilityId, CancellationToken cancellationToken)
    {
        ReadyToAcquire messageValue = null;
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dataAcquisitionLogManager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();
            var fhirQueryConfigurationManager = scope.ServiceProvider.GetRequiredService<IFhirQueryConfigurationManager>();
            var dataAcquisitionLogQueries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

            var config = await fhirQueryConfigurationManager.GetAsync(facilityId, cancellationToken);

            const int pageSize = 100;
            var baseMessage = "Request FAILED due to missing FhirQueryConfiguration. FacilityId:";
            if (config == null)
            {
                _logger.LogCritical("{baseMessage} {facilityId}", baseMessage.Sanitize(), facilityId.Sanitize());

                int page = 1;
                while (true)
                {
                    var requests = await dataAcquisitionLogQueries.GetPendingAndRetryableFailedRequestsForFacility(facilityId, page, pageSize, cancellationToken);
                    if (!requests.Any()) break;

                    foreach (var request in requests)
                    {
                        request.Status = RequestStatus.Failed;
                        request.Notes ??= new List<string>();
                        request.Notes.Add($"{baseMessage} {request.FacilityId}.");
                        await dataAcquisitionLogManager.UpdateAsync(request, cancellationToken);
                    }

                    page++;
                }

                return;
            }

            if (!IsWithinAcquisitionWindow(config.MinAcquisitionPullTime, config.MaxAcquisitionPullTime))
            {
                _logger.LogInformation("Current time {currentTime} is outside the acquisition window for facility {facilityId}.", DateTime.UtcNow.TimeOfDay, facilityId);
                return;
            }

            int pageNumber = 1;
            while (true)
            {
                var requests = await dataAcquisitionLogQueries.GetPendingAndRetryableFailedRequestsForFacility(facilityId, pageNumber, pageSize, cancellationToken);
                if (!requests.Any()) break;

                _logger.BeginScope("Processing {count} processable requests for facility {facilityId}", requests.Count, facilityId);

                foreach (var request in requests)
                {
                    request.RetryAttempts ??= 0;
                    request.Notes ??= new List<string>();

                    if (request.Status == RequestStatus.Failed)
                    {
                        if (request.RetryAttempts >= 10)
                        {
                            request.Status = RequestStatus.MaxRetriesReached;
                            request.Notes.Add($"[{DateTime.UtcNow}] Maximum retry attempts (10) reached for request.");
                            await dataAcquisitionLogManager.UpdateAsync(request, cancellationToken);
                            continue;
                        }

                        request.RetryAttempts += 1;
                        request.Notes.Add($"[{DateTime.UtcNow}] Retrying failed request. Attempt {request.RetryAttempts}.");
                    }

                    messageValue = new ReadyToAcquire { FacilityId = facilityId, LogId = request.Id };

                    _logger.LogInformation("Generating ReadyToAcquire message for log id: {request.Id}", request.Id.Sanitize());

                    request.Status = RequestStatus.Ready;
                    await dataAcquisitionLogManager.UpdateAsync(request, cancellationToken);

                    try
                    {
                        _logger.LogInformation("Producing ReadyToAcquire message for log id: {logId} and facility id: {facilityId}", request.Id.Sanitize(), facilityId.Sanitize());

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
                                    FacilityId = facilityId,
                                    ReportTrackingId = request.ReportTrackingId
                                },
                                Headers = headers
                            }, cancellationToken);
                        _readyToAcquireProducer.Flush(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error producing ReadyToAcquire message for log id: {logId}", request.Id.Sanitize());

                        request.Status = RequestStatus.Failed;
                        request.Notes.Add($"[{DateTime.UtcNow}] Failed to produce ReadyToAcquire message: {ex.Message}");
                        await dataAcquisitionLogManager.UpdateAsync(request, cancellationToken);
                    }

                    messageValue = null;
                }

                pageNumber++;
            }

            _logger.LogInformation("Completed processing processable requests for facility {facilityId}.", facilityId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing acquisition job for facility id: {facilityId}", facilityId);
            _transientExceptionHandler.HandleException(ex, messageValue, facilityId, $"Error processing acquisition job for facility id: {facilityId}");
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

    private async Task ProcessPendingTailingMessages(CancellationToken cancellationToken)
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

                    await dataAcquisitionLogManager.UpdateTailFlagForFacilityCorrelationIdReportTrackingId(
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