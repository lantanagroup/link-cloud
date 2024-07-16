using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using MediatR;

namespace LantanaGroup.Link.DataAcquisition.Application.Services;

public class PatientCensusScheduledProcessingLogic : IConsumerLogic<string, PatientCensusScheduled, string, PatientIDsAcquiredMessage>
{
    private readonly ILogger<PatientCensusScheduledProcessingLogic> _logger;
    private readonly IPatientCensusService _patientCensusService;
    private readonly IProducer<string, PatientIDsAcquiredMessage> _kafkaProducer;

    public PatientCensusScheduledProcessingLogic(
        ILogger<PatientCensusScheduledProcessingLogic> logger,
        IPatientCensusService patientCensusService, 
        IProducer<string, PatientIDsAcquiredMessage> kafkaProducer
        )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _patientCensusService = patientCensusService ?? throw new ArgumentNullException(nameof(patientCensusService));
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

        try
        {
            facilityId = extractFacilityId(consumeResult);
        }
        catch (MissingFacilityIdException ex)
        {
            _logger.LogError(ex, "FacilityId is missing from the message key.");
            throw new DeadLetterException("FacilityId is missing from the message key.", AuditEventType.Query, ex);
        }

        IBaseMessage? result;

        try
        {
            result = await _patientCensusService.Get(facilityId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while processing the message.");
            throw new TransientException("Error occurred while processing the message.", AuditEventType.Query, ex);
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
                throw new TransientException("An error producing a PatientIdsAcquiredMessage", AuditEventType.Query, ex);
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

