using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Shared.Application.Interfaces;
public interface IKafkaProducerFactory<TProducerKey, TProducerValue>
{
    public IProducer<string, AuditEventMessage> CreateAuditEventProducer(bool useOpenTelemetry = true);
    public IProducer<TProducerKey, TProducerValue> CreateProducer(ProducerConfig config, ISerializer<TProducerKey>? keySerializer = null, ISerializer<TProducerValue>? valueSerializer = null, bool useOpenTelemetry = true);
}
