using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Services.FhirApi.Commands;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using Quartz;

namespace LantanaGroup.Link.DataAcquisition.Application.Jobs;

[DisallowConcurrentExecution]
public class AcquisitionProcessingJob : IJob
{
    private readonly ILogger<AcquisitionProcessingJob> _logger;
    private readonly IReadFhirCommand _readFhirCommand;
    private readonly ISearchFhirCommand _searchFhirCommand;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IProducer<string, ReadyToAcquire> _readyToAcquireProducer;
    protected readonly ITransientExceptionHandler<Null, ReadyToAcquire> _transientExceptionHandler;

    public AcquisitionProcessingJob(
        ILogger<AcquisitionProcessingJob> logger,
        IReadFhirCommand readFhirCommand,
        ISearchFhirCommand searchFhirCommand,
        IServiceScopeFactory serviceScopeFactory,
        IProducer<string, ReadyToAcquire> readyToAcquireProducer,
        ITransientExceptionHandler<Null, ReadyToAcquire> transientExceptionHandler)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _readFhirCommand = readFhirCommand ?? throw new ArgumentNullException(nameof(readFhirCommand));
        _searchFhirCommand = searchFhirCommand ?? throw new ArgumentNullException(nameof(searchFhirCommand));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _readyToAcquireProducer = readyToAcquireProducer ?? throw new ArgumentNullException(nameof(readyToAcquireProducer));
        _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentNullException(nameof(transientExceptionHandler));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        string? facilityId = string.Empty;
        ReadyToAcquire messageValue = null;
        try
        {
            //set scope for DataAcquisitionLogManager
            using var scope = _serviceScopeFactory.CreateScope();
            var _dataAcquisitionLogManager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();

            //get pending requests
            var pendingRequests = await _dataAcquisitionLogManager.GetPendingRequests();

            //process each request
            foreach (var request in pendingRequests)
            {
                //set facility id
                facilityId = request.FacilityId;
                messageValue = new ReadyToAcquire { FacilityId = facilityId, LogId = request.Id };

                //process request
                _logger.LogInformation($"Generating ReadyToAcquire message for log id: {request.Id}");

                await _readyToAcquireProducer.ProduceAsync(
                    KafkaTopic.ReadyToAcquire.ToString(),
                    new Message<string, ReadyToAcquire>
                    {
                        Key = request.Id,
                        Value = new ReadyToAcquire
                        {
                            LogId = request.Id,
                            FacilityId = request.FacilityId
                        }
                    });

                facilityId = string.Empty;
                messageValue = null;
            }
        }
        catch (Exception ex) 
        {
            _logger.LogError(ex, "Error processing acquisition job for facility id: {facilityId}", facilityId);
            _transientExceptionHandler.HandleException(ex, messageValue, facilityId, $"Error processing acquisition job for facility id: {facilityId}");
        }
    }
}
