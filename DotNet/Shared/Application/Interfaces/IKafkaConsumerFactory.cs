using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Shared.Application.Interfaces;
public interface IKafkaConsumerFactory<TConsumerKey, TConsumerValue>
{
    public IConsumer<TConsumerKey, TConsumerValue> CreateConsumer(
        ConsumerConfig consumerConfig,
        IDeserializer<TConsumerKey>? keyDeserializer = null,
        IDeserializer<TConsumerValue>? valueDeserializer = null,
        KafkaAssignmentTracker? assignmentTracker = null);
}
