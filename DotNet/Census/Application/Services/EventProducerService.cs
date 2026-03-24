using Confluent.Kafka;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Application.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using System.Text;

namespace LantanaGroup.Link.Census.Application.Services;

public interface IEventProducerService<MessageType> 
{
    Task ProduceEventsAsync(string key, IEnumerable<IBaseResponse> events, CancellationToken cancellationToken = default);
}

public class EventProducerService<MessageType> : IEventProducerService<MessageType> 
{
    private readonly ILogger<EventProducerService<MessageType>> _logger;
    private readonly IProducer<string, MessageType> _kafkaProducer;

    public EventProducerService(ILogger<EventProducerService<MessageType>> logger, IProducer<string, MessageType> kafkaProducer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _kafkaProducer = kafkaProducer ?? throw new ArgumentNullException(nameof(kafkaProducer));
    }

    public async Task ProduceEventsAsync(string key, IEnumerable<IBaseResponse> events, CancellationToken cancellationToken = default)
    {
        foreach (var ev in events)
        {
            if (ev is PatientEventResponse patientEventResponse)
            {
                //We are not going to generate an PatienEvent messages that are marked as 'Update'. We should only process admits and discharges.
                if (patientEventResponse.PatientEvent.EventType == PatientEvents.Update.ToString()) 
                {
                    continue;
                }

                Headers? headers = null;
                if (patientEventResponse.CorrelationId != null)
                    headers = new Headers
                    {
                        new Header(CensusConstants.HeaderNames.CorrelationId, Encoding.UTF8.GetBytes(patientEventResponse.CorrelationId))
                    };

                // Cast the PatientEvent to MessageType to match the generic type constraint
                var message = new Message<string, MessageType>
                {
                    Key = patientEventResponse.FacilityId,
                    Headers = headers ?? null,
                    Value = (MessageType)(object)patientEventResponse.PatientEvent
                };

                try 
                {
                    _logger.LogDebug("Producing PatientEvent (Facility = {FacilityId}, PatientId = {PatientId}, Event = {Event})", patientEventResponse.FacilityId, patientEventResponse.PatientEvent.PatientId, patientEventResponse.PatientEvent.EventType);
                    await _kafkaProducer.ProduceAsync(KafkaTopic.PatientEvent.ToString(), message, cancellationToken);
                }
                catch (ProduceException<string, MessageType> ex)
                {
                   _logger.LogError(ex, "EventProducerService: Error producing event to Kafka topic {Topic} for facility: {FacilityId}.", KafkaTopic.PatientEvent.ToString(), patientEventResponse.FacilityId);
                    throw;
                }
            }
        }
    }
}
