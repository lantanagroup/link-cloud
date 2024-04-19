using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.SerDes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Conventions;

namespace LantanaGroup.Link.Shared.Application.Wrappers;

[Obsolete("This is deprecated. Please use KafkaProducer and KafkaConsumer instead.")]
public class KafkaWrapper<ConsumerKey, ConsumerValue, ProducerKey, ProducerValue>
        : IKafkaWrapper<ConsumerKey, ConsumerValue, ProducerKey, ProducerValue>
{
    private readonly ILogger<KafkaWrapper<ConsumerKey, ConsumerValue, ProducerKey, ProducerValue>> _logger;
    protected readonly IOptions<KafkaConnection> _kafkaConnection;
    private readonly IConsumer<ConsumerKey, ConsumerValue> _consumer;
    private readonly IProducer<ProducerKey, ProducerValue> _producer;

    public KafkaWrapper(ILogger<KafkaWrapper<ConsumerKey, ConsumerValue, ProducerKey, ProducerValue>> logger, IOptions<KafkaConnection> kafkaConnection)
    {
        this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this._kafkaConnection = kafkaConnection ?? throw new ArgumentNullException(nameof(KafkaConnection));
        _consumer = buildConsumer() ?? throw new ArgumentNullException(nameof(_consumer));
        _producer = buildProducer() ?? throw new ArgumentNullException(nameof(_producer));
    }

    public ConsumerValue ConsumeKafkaMessage(CancellationToken cancellationToken)
    {
        var consumeResult = _consumer.Consume(cancellationToken);
        return consumeResult.Message.Value;
    }

    public ConsumeResult<ConsumerKey, ConsumerValue> ConsumeAndReturnFullMessage(CancellationToken cancellationToken = default)
    {
        try
        {
            return _consumer.Consume(cancellationToken);
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    public (string topic, ConsumerValue value) ConsumeKafkaMessageWithTopic(CancellationToken cancellationToken)
    {
        var consumeResult = _consumer.Consume(cancellationToken);
        return (consumeResult.Topic, consumeResult.Message.Value);
    }

    public async Task ProduceKafkaMessageAsync(string topic, Func<Message<ProducerKey, ProducerValue>> messageGenerator)
    {
        await _producer.ProduceAsync(topic, messageGenerator());
        _producer.Flush();
    }

    public void ProduceKafkaMessage(string topic, Func<Message<ProducerKey, ProducerValue>> messageGenerator)
    {
        _producer.Produce(topic, messageGenerator());
        _producer.Flush();
    }

    public void SubscribeToKafkaTopic(IEnumerable<string> topics)
    {
        _consumer.Subscribe(topics);
    }

    public void DisposeConsumer()
    {
        _consumer.Dispose();
    }

    public void DisposeProducer()
    {
        _producer.Dispose();
    }

    public void CloseConsumer()
    {
        _consumer.Close();
    }

    protected virtual IConsumer<ConsumerKey, ConsumerValue> buildConsumer()
    {
        if (typeof(ConsumerValue) == typeof(string))
        {
            return new ConsumerBuilder<ConsumerKey, ConsumerValue>(_kafkaConnection.Value.CreateConsumerConfig())
            .Build();
        }

        return new ConsumerBuilder<ConsumerKey, ConsumerValue>(_kafkaConnection.Value.CreateConsumerConfig())
            .SetValueDeserializer(new JsonWithFhirMessageDeserializer<ConsumerValue>())
            .Build();
    }

    protected virtual IProducer<ProducerKey, ProducerValue> buildProducer()
    {
        if (typeof(ProducerValue) == typeof(string))
        {
            return new ProducerBuilder<ProducerKey, ProducerValue>(_kafkaConnection.Value.CreateProducerConfig())
            .Build();
        }

        return new ProducerBuilder<ProducerKey, ProducerValue>(_kafkaConnection.Value.CreateProducerConfig())
            .SetValueSerializer(new JsonWithFhirMessageSerializer<ProducerValue>())
            .Build();
    }
}