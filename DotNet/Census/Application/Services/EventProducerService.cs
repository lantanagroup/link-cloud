using Confluent.Kafka;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Messages;
using LantanaGroup.Link.Census.Application.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using Quartz;
using System.Text;

namespace LantanaGroup.Link.Census.Application.Services;

public interface IEventProducerService<TKey, TValue>
{
    //Task ProduceEventAsync<T>(T eventMessage, string topic, CancellationToken cancellationToken = default) where T : class;
    Task ProduceEventsAsync(IEnumerable<BaseResponse> events, CancellationToken cancellationToken = default);
}
public class EventProducerService<TKey, TValue> : IEventProducerService<TKey, TValue>
{
    private readonly IProducer<TKey, TValue> _kafkaProducer;

    public EventProducerService(IProducer<TKey, TValue> kafkaProducer)
    {
        _kafkaProducer = kafkaProducer ?? throw new ArgumentNullException(nameof(kafkaProducer));
    }

    public async Task ProduceEventsAsync(IEnumerable<BaseResponse> events, CancellationToken cancellationToken = default)
    {
        foreach (var ev in events)
        {
            if (ev.TopicName == KafkaTopic.PatientEvent.ToString())
            {
                if (((PatientEventResponse)ev).PatientEvent == null) return;
                PatientEvent? patientEvent = ((PatientEventResponse)ev).PatientEvent;

                Headers? headers = null;
                if (ev.CorrelationId != null)
                    headers = new Headers
                        {
                            new Header(CensusConstants.HeaderNames.CorrelationId, Encoding.UTF8.GetBytes(ev.CorrelationId))
                        };
                var message = new Message<string, object>
                {
                    Key = ev.FacilityId,
                    Headers = headers ?? null,
                    Value = patientEvent
                };

                await _kafkaProducer.ProduceAsync(KafkaTopic.PatientEvent.ToString(), message);
            }
        }
    }
}
