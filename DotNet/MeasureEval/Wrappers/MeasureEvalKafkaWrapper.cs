using Confluent.Kafka;
using LantanaGroup.Link.MeasureEval.Models;
using LantanaGroup.Link.MeasureEval.Serializers;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.SerDes;
using LantanaGroup.Link.Shared.Application.Wrappers;

namespace LantanaGroup.Link.MeasureEval.Wrappers;

public class MeasureEvalKafkaWrapper<ConsumerKey, ConsumerValue, ProducerKey, ProducerValue>
    : KafkaWrapper<ConsumerKey, ConsumerValue, ProducerKey, ProducerValue>
    where ConsumerValue : PatientDataNormalizedMessage
    where ProducerKey : PatientDataEvaluatedKey
    where ProducerValue : PatientDataEvaluatedMessage
{
    public MeasureEvalKafkaWrapper(ILogger<KafkaWrapper<ConsumerKey, ConsumerValue, ProducerKey, ProducerValue>> logger, KafkaConnection kafkaConnectionSettings) 
        : base(logger, kafkaConnectionSettings)
    {
    }

    protected override IConsumer<ConsumerKey, ConsumerValue> buildConsumer()
    {
        return new ConsumerBuilder<ConsumerKey, ConsumerValue>(kafkaConnectionSettings.CreateConsumerConfig())
            .SetValueDeserializer(new PatientDataNormalizedMessageDeserializer<ConsumerValue>())
            .Build();
    }

    protected override IProducer<ProducerKey, ProducerValue> buildProducer()
    {
        if (typeof(ProducerValue) is string)
        {
            return new ProducerBuilder<ProducerKey, ProducerValue>(kafkaConnectionSettings.CreateProducerConfig())
            .Build();
        }

        return new ProducerBuilder<ProducerKey, ProducerValue>(kafkaConnectionSettings.CreateProducerConfig())
            .SetKeySerializer (new JsonWithFhirMessageSerializer<ProducerKey>())
            .SetValueSerializer(new PatientDataEvaluatedMessageSerializer<ProducerValue>())
            .Build();
    }
}
