using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.SerDes;
using Microsoft.Extensions.Logging;
using MongoDB.Bson.Serialization.Conventions;

namespace LantanaGroup.Link.Shared.Application.Wrappers;

[Obsolete("This is deprecated. Please use KafkaProducer and KafkaConsumer instead.")]
public class KafkaWrapper<ConsumerKey, ConsumerValue, ProducerKey, ProducerValue>
        : IKafkaWrapper<ConsumerKey, ConsumerValue, ProducerKey, ProducerValue>
{
    private readonly ILogger<KafkaWrapper<ConsumerKey, ConsumerValue, ProducerKey, ProducerValue>> logger;
    protected readonly KafkaConnection kafkaConnectionSettings;
    private readonly IConsumer<ConsumerKey, ConsumerValue> consumer;
    private readonly IProducer<ProducerKey, ProducerValue> producer;

    public KafkaWrapper(ILogger<KafkaWrapper<ConsumerKey, ConsumerValue, ProducerKey, ProducerValue>> logger, KafkaConnection kafkaConnectionSettings)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.kafkaConnectionSettings = kafkaConnectionSettings ?? throw new ArgumentNullException(nameof(KafkaConnection));
        consumer = buildConsumer() ?? throw new ArgumentNullException(nameof(consumer));
        producer = buildProducer() ?? throw new ArgumentNullException(nameof(producer));
    }

    public ConsumerValue ConsumeKafkaMessage(CancellationToken cancellationToken)
    {
        var consumeResult = consumer.Consume(cancellationToken);
        return consumeResult.Message.Value;
    }

    public ConsumeResult<ConsumerKey, ConsumerValue> ConsumeAndReturnFullMessage(CancellationToken cancellationToken = default)
    {
        try
        {
            return consumer.Consume(cancellationToken);
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    public (string topic, ConsumerValue value) ConsumeKafkaMessageWithTopic(CancellationToken cancellationToken)
    {
        var consumeResult = consumer.Consume(cancellationToken);
        return (consumeResult.Topic, consumeResult.Message.Value);
    }

    public async Task ProduceKafkaMessageAsync(string topic, Func<Message<ProducerKey, ProducerValue>> messageGenerator)
    {
        await producer.ProduceAsync(topic, messageGenerator());
        producer.Flush();
    }

    public void ProduceKafkaMessage(string topic, Func<Message<ProducerKey, ProducerValue>> messageGenerator)
    {
        producer.Produce(topic, messageGenerator());
        producer.Flush();
    }

    public void SubscribeToKafkaTopic(IEnumerable<string> topics)
    {
        consumer.Subscribe(topics);
    }

    public void DisposeConsumer()
    {
        consumer.Dispose();
    }

    public void DisposeProducer()
    {
        producer.Dispose();
    }

    public void CloseConsumer()
    {
        consumer.Close();
    }

    protected virtual IConsumer<ConsumerKey, ConsumerValue> buildConsumer()
    {
        if (typeof(ConsumerValue) == typeof(string))
        {
            return new ConsumerBuilder<ConsumerKey, ConsumerValue>(kafkaConnectionSettings.CreateConsumerConfig())
            .Build();
        }

        return new ConsumerBuilder<ConsumerKey, ConsumerValue>(kafkaConnectionSettings.CreateConsumerConfig())
            .SetValueDeserializer(new JsonWithFhirMessageDeserializer<ConsumerValue>())
            .Build();
    }

    protected virtual IProducer<ProducerKey, ProducerValue> buildProducer()
    {
        if (typeof(ProducerValue) == typeof(string))
        {
            return new ProducerBuilder<ProducerKey, ProducerValue>(kafkaConnectionSettings.CreateProducerConfig())
            .Build();
        }

        return new ProducerBuilder<ProducerKey, ProducerValue>(kafkaConnectionSettings.CreateProducerConfig())
            .SetValueSerializer(new JsonWithFhirMessageSerializer<ProducerValue>())
            .Build();
    }
}