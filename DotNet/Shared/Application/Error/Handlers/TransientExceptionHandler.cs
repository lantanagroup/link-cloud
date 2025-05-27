using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.Extensions.Logging;
using System.Text;

namespace LantanaGroup.Link.Shared.Application.Error.Handlers
{
    public class TransientExceptionHandler<K, V> : ITransientExceptionHandler<K, V>
    {
        protected readonly ILogger<TransientExceptionHandler<K, V>> Logger;
        protected readonly IKafkaProducerFactory<K, V> ProducerFactory;

        public string Topic { get; set; } = string.Empty;

        public string ServiceName { get; set; } = string.Empty;

        public TransientExceptionHandler(ILogger<TransientExceptionHandler<K, V>> logger,
            IKafkaProducerFactory<K, V> producerFactory)
        {
            Logger = logger;
            ProducerFactory = producerFactory;
        }

        public void HandleException(ConsumeResult<K, V> consumeResult, string facilityId, string message = "")
        {
            try
            {
                Logger.LogError("{Name}: Failed to process {S} Event: {Message}", GetType().Name, ServiceName, message);

                ProduceRetryScheduledEvent(consumeResult.Message.Key, consumeResult.Message.Value,
                    consumeResult.Message.Headers, facilityId, message);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error in {Name}.HandleException: {Message}", GetType().Name, message);
                throw;
            }
        }

        public virtual void HandleException(ConsumeResult<K, V> consumeResult, Exception ex, string facilityId)
        {
            var tEx = new TransientException(ex.Message, ex.InnerException);
            HandleException(consumeResult, tEx, facilityId);
        }

        public virtual void HandleException(ConsumeResult<K, V> consumeResult, TransientException ex, string facilityId)
        {
            try
            {
                Logger.LogError(ex, "{Name}: Failed to process {S} Event.", GetType().Name, ServiceName);

                ProduceRetryScheduledEvent(consumeResult.Message.Key, consumeResult.Message.Value,
                    consumeResult.Message.Headers, facilityId, ex.Message, ex.StackTrace ?? string.Empty);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error in {Name}.HandleException: {Message}", GetType().Name, e.Message);
                throw;
            }
        }

        public virtual void ProduceRetryScheduledEvent(K key, V value, Headers headers, string facilityId, string message = "", string stackTrace = "")
        {
            if (string.IsNullOrWhiteSpace(Topic))
            {
                throw new Exception(
                    $"{GetType().Name}.Topic has not been configured. Cannot Produce Retry Event for {ServiceName}");
            }

            headers ??= [];

            if (!headers.TryGetLastBytes(KafkaConstants.HeaderConstants.ExceptionService, out var headerValue))
            {
                headers.Add(KafkaConstants.HeaderConstants.ExceptionService, Encoding.UTF8.GetBytes(ServiceName));
            }


            if (headers.TryGetLastBytes(KafkaConstants.HeaderConstants.RetryExceptionMessage, out var exceptionValue))
            {
                headers.Remove(KafkaConstants.HeaderConstants.RetryExceptionMessage);
            }

            headers.Add(KafkaConstants.HeaderConstants.RetryExceptionMessage, Encoding.UTF8.GetBytes(message + Environment.NewLine + stackTrace));
            

            if (!string.IsNullOrEmpty(facilityId) && !headers.TryGetLastBytes(KafkaConstants.HeaderConstants.ExceptionFacilityId, out var topicValue))
            {
                headers.Add(KafkaConstants.HeaderConstants.ExceptionFacilityId, Encoding.UTF8.GetBytes(facilityId));
            }

            using var producer = ProducerFactory.CreateProducer(new ProducerConfig(), useOpenTelemetry: false);
            producer.Produce(Topic, new Message<K, V>
            {
                Key = key,
                Value = value,
                Headers = headers
            });

            producer.Flush();
        }

 
    }
}