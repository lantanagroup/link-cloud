using Confluent.Kafka;

namespace LantanaGroup.Link.Shared.Application.Wrappers;

[Obsolete("This is deprecated. Please use IKafkaProducer and IKafkaConsumer instead.")]
public interface IKafkaWrapper<ConsumerKey, ConsumerValue, ProducerKey, ProducerValue>
{
    ConsumerValue ConsumeKafkaMessage(CancellationToken cancellationToken);
    ConsumeResult<ConsumerKey, ConsumerValue> ConsumeAndReturnFullMessage(CancellationToken cancellationToken = default);
    (string topic, ConsumerValue value) ConsumeKafkaMessageWithTopic(CancellationToken cancellationToken);
    Task ProduceKafkaMessageAsync(string topic, Func<Message<ProducerKey, ProducerValue>> messageGenerator);
    void ProduceKafkaMessage(string topic, Func<Message<ProducerKey, ProducerValue>> messageGenerator);
    void SubscribeToKafkaTopic(IEnumerable<string> topics);
    void DisposeConsumer();
    void DisposeProducer();
    void CloseConsumer();

    ConsumerValue ConsumeKafkaMessage()
    {
        return ConsumeKafkaMessage(CancellationToken.None);
    }

    (string topic, ConsumerValue value) ConsumeKafkaMessageWithTopic()
    {
        return ConsumeKafkaMessageWithTopic(CancellationToken.None);
    }
}