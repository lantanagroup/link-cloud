using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;

namespace LantanaGroup.Link.Shared.Application.Error.Interfaces
{
    public interface ITransientExceptionHandler<T, K, V>
    {
        /// <summary>
        /// The Topic to use when publishing Retry Kafka events.
        /// </summary>
        public string Topic { get; set; }

        void HandleException(ConsumeResult<K, V> consumeResult, Exception ex, string facilityId);
        void HandleException(ConsumeResult<K, V> consumeResult, TransientException ex, string facilityId);
        void ProduceRetryScheduledEvent(K key, V value, Headers headers, string facilityId, string message = "", string stackTrace = "");
    }
}
