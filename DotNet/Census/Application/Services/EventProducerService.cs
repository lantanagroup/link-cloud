using Confluent.Kafka;
using LantanaGroup.Link.Census.Application.Models;
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
    private readonly IProducer<string, MessageType> _kafkaProducer;

    public EventProducerService(IProducer<string, MessageType> kafkaProducer)
    {
        _kafkaProducer = kafkaProducer ?? throw new ArgumentNullException(nameof(kafkaProducer));
    }

    public async Task ProduceEventsAsync(string key, IEnumerable<IBaseResponse> events, CancellationToken cancellationToken = default)
    {
        foreach (var ev in events)
        {
            if (ev is PatientEventResponse patientEventResponse)
            {
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

                await _kafkaProducer.ProduceAsync(KafkaTopic.PatientEvent.ToString(), message);
            }
        }
    }
}
