using Confluent.Kafka;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Messages;
using LantanaGroup.Link.Census.Application.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using Quartz;
using System.Text;

namespace LantanaGroup.Link.Census.Application.Services;

public interface IEventProducerService<TKey, TValue> where TValue : BaseResponse
{
    //Task ProduceEventAsync<T>(T eventMessage, string topic, CancellationToken cancellationToken = default) where T : class;
    Task ProduceEventsAsync(TKey key, IEnumerable<TValue> events, string? correlationId = default, CancellationToken cancellationToken = default);
}
public class EventProducerService<TKey, TValue> : IEventProducerService<TKey, TValue> where TValue : BaseResponse
{
    private readonly IProducer<TKey, TValue> _kafkaProducer;

    public EventProducerService(IProducer<TKey, TValue> kafkaProducer)
    {
        _kafkaProducer = kafkaProducer ?? throw new ArgumentNullException(nameof(kafkaProducer));
    }

    public async Task ProduceEventsAsync(TKey key, IEnumerable<TValue> events, string? correlationId = default, CancellationToken cancellationToken = default)
    {
        foreach (var ev in events)
        {
            if(ev is PatientEventResponse patientEventResponse && key is string)
            {
                Headers? headers = null;
                if (patientEventResponse.CorrelationId != null)
                    headers = new Headers
                        {
                            new Header(CensusConstants.HeaderNames.CorrelationId, Encoding.UTF8.GetBytes(patientEventResponse.CorrelationId))
                        };
                var message = new Message<string, PatientEvent>
                {
                    Key = patientEventResponse.FacilityId,
                    Headers = headers ?? null,
                    Value = patientEventResponse.PatientEvent
                };

                //await _kafkaProducer.ProduceAsync(KafkaTopic.PatientEvent.ToString(), message);
            }
        }
    }
}
