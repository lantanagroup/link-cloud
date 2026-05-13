using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;

namespace LantanaGroup.Link.Shared.Application.Error.Interfaces
{
    public interface IDeadLetterExceptionHandler<T, K, V>
    {
        /// <summary>
        /// The Topic to use when publishing Retry Kafka events.
        /// </summary>
        public string Topic { get; set; }

        void HandleException(ConsumeResult<K, V> consumeResult, string facilityId, string message = "");
        void HandleException(ConsumeResult<K, V> consumeResult, Exception ex, string facilityId);
        void HandleException(ConsumeResult<K, V> consumeResult, DeadLetterException ex, string facilityId);
        void HandleConsumeException(ConsumeException ex, string facilityId);
        void ProduceDeadLetter(ConsumeResult<K, V> consumeResult, string exceptionMessage);
    }
}
