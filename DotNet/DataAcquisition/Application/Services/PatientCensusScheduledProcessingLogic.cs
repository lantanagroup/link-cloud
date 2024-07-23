using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.DataAcquisition.Application.Services;

public class PatientCensusScheduledProcessingLogic : IConsumerLogic<string, PatientCensusScheduled, string, PatientIDsAcquiredMessage>
{
    private readonly ILogger<PatientCensusScheduledProcessingLogic> _logger;
    private readonly IPatientCensusService _patientCensusService;
    private readonly IProducer<string, PatientIDsAcquiredMessage> _kafkaProducer;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public PatientCensusScheduledProcessingLogic(
        ILogger<PatientCensusScheduledProcessingLogic> logger,
        IServiceScopeFactory serviceScopeFactory,
        IProducer<string, PatientIDsAcquiredMessage> kafkaProducer
        )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _kafkaProducer = kafkaProducer ?? throw new ArgumentNullException(nameof(kafkaProducer));
    }

    public ConsumerConfig createConsumerConfig()
    {
        var settings = new ConsumerConfig
        {
            EnableAutoCommit = false,
            GroupId = ServiceActivitySource.ServiceName
        };
        return settings;
    }

    public async Task executeLogic(
        ConsumeResult<string, PatientCensusScheduled> consumeResult, 
        CancellationToken cancellationToken = default, 
        params object[] optionalArgList
        )
    {
        string? facilityId;


        var scope = _serviceScopeFactory.CreateScope();
        var patientCensusService = scope.ServiceProvider.GetRequiredService<IPatientCensusService>();

        try
        {
            facilityId = extractFacilityId(consumeResult);
        }
        catch (MissingFacilityIdException ex)
        {
            _logger.LogError(ex, "FacilityId is missing from the message key.");
            throw new DeadLetterException("FacilityId is missing from the message key.", ex);
        }

        IBaseMessage? result;

        try
        {
            result = await patientCensusService.Get(facilityId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while processing the message.");
            throw new TransientException("Error occurred while processing the message.", ex);
        }

        if(result != null)
        {
            
            var produceMessage = new Message<string, PatientIDsAcquiredMessage>
            {
                Key = facilityId,
                Value = (PatientIDsAcquiredMessage)result
            };

            try
            {
                await _kafkaProducer.ProduceAsync(KafkaTopic.PatientIDsAcquired.ToString(), produceMessage, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new TransientException("An error producing a PatientIdsAcquiredMessage", ex);
            }
        }
    }

    public string extractFacilityId(ConsumeResult<string, PatientCensusScheduled> consumeResult)
    {
        if(string.IsNullOrWhiteSpace(consumeResult.Message.Key))
        {
            throw new MissingFacilityIdException("FacilityId is missing from the message key.");
        }
        return consumeResult.Message.Key;
    }
}

