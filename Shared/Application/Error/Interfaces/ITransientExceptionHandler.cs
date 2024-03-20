using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Shared.Application.Error.Interfaces
{
    public interface ITransientExceptionHandler<K, V>
    {
        void HandleException(ConsumeResult<K, V> consumeResult, Exception ex);
        void ProduceAuditEvent(IKafkaProducerFactory<string, AuditEventMessage> auditProducerFactory, AuditEventMessage auditValue, Headers headers);
        void ProduceRetryScheduledEvent(K key, V value, Headers headers);
    }
}
