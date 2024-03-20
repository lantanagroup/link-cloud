using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Shared.Application.Error.Interfaces
{
    public interface IDeadLetterExceptionHandler<K, V>
    {
        /// <summary>
        /// The Topic to use when publishing Retry Kafka events.
        /// </summary>
        public string Topic { get; set; }

        /// <summary>
        /// The name of the service that is consuming the IDeadLetterExceptionHandler.
        /// </summary>
        public string ServiceName { get; set; }

        void HandleException(ConsumeResult<K, V> consumeResult, Exception ex);
        void ProduceAuditEvent(AuditEventMessage auditValue, Headers headers);
        void ProduceDeadLetter(K key, V value, Headers headers, string exceptionMessage);
    }
}
