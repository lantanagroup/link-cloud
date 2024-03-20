using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Shared.Application.Error.Interfaces
{
    public interface IConsumerExceptionHandler<K, V>
    {
        void HandleException(ConsumeResult<K, V> consumeResult, Exception ex);
        void ProduceAuditEvent(AuditEventMessage auditValue, Headers headers);
        void ProduceEvent(K key, V value, Headers headers);
    }
}
