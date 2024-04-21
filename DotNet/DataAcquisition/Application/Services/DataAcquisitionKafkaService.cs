using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Serializers;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Wrappers;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.DataAcquisition.Services;

public class DataAcquisitionKafkaService<ConsumerKey, ConsumerValue, ProducerKey, ProducerValue>
    : KafkaWrapper<ConsumerKey, ConsumerValue, ProducerKey, ProducerValue>
{
    public DataAcquisitionKafkaService(ILogger<KafkaWrapper<ConsumerKey, ConsumerValue, ProducerKey, ProducerValue>> logger, IOptions<KafkaConnection> kafkaConnection) 
        : base(logger, kafkaConnection)
    {
    }

    protected override IConsumer<ConsumerKey, ConsumerValue> buildConsumer()
    {
        return base.buildConsumer();
    }

    protected override IProducer<ProducerKey, ProducerValue> buildProducer()
    {
        if (typeof(ProducerValue) is string)
        {
            return new ProducerBuilder<ProducerKey, ProducerValue>(_kafkaConnection.Value.CreateProducerConfig())
            .Build();
        }

        return new ProducerBuilder<ProducerKey, ProducerValue>(_kafkaConnection.Value.CreateProducerConfig())
            .SetValueSerializer(new PatientIDsAcquiredDataSerializer<ProducerValue>())
            .Build();
    }
}
