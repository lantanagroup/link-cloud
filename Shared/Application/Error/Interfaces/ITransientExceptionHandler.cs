using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Shared.Application.Error.Interfaces
{
    public interface ITransientExceptionHandler<K, V>
    {
        /// <summary>
        /// The Topic to use when publishing Retry Kafka events.
        /// </summary>
        public string Topic { get; set; }

        /// <summary>
        /// The name of the service that is consuming the ITransientExceptionHandler.
        /// </summary>
        public string ServiceName { get; set; }

        void HandleException(ConsumeResult<K, V> consumeResult, Exception ex);
        void ProduceAuditEvent(AuditEventMessage auditValue, Headers headers);
        void ProduceRetryScheduledEvent(K key, V value, Headers headers);
    }
}
