using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Text;

namespace LantanaGroup.Link.Shared.Application.Error.Handlers
{
    // TODO: Remove unused facility ID parameters?
    public class TransientExceptionHandler<K, V> : ITransientExceptionHandler<K, V>
    {
        protected readonly ILogger<TransientExceptionHandler<K, V>> Logger;
        protected readonly IKafkaProducerFactory<K, V> ProducerFactory;
        protected readonly ServiceInformation ServiceInformation;

        public string Topic { get; set; } = string.Empty;

        protected string ServiceName { get; set; } = string.Empty;

        public TransientExceptionHandler(ILogger<TransientExceptionHandler<K, V>> logger,
            IKafkaProducerFactory<K, V> producerFactory,
            ServiceInformation serviceInformation)
        {
            Logger = logger;
            ProducerFactory = producerFactory;
            ServiceInformation = serviceInformation;

            ServiceName = ServiceInformation.ServiceConfigName ?? throw new ArgumentNullException("ServiceName must be populated");
        }

        public virtual void HandleException(ConsumeResult<K, V> consumeResult, Exception ex, string facilityId)
        {
            var tEx = new TransientException(ex.Message, ex);
            HandleException(consumeResult, tEx, facilityId);
        }

        public virtual void HandleException(ConsumeResult<K, V> consumeResult, TransientException ex, string facilityId)
        {
            try
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                
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

            //Exception Message
            if (headers.TryGetLastBytes(KafkaConstants.HeaderConstants.RetryExceptionMessage, out var exceptionValue))
            {
                headers.Remove(KafkaConstants.HeaderConstants.RetryExceptionMessage);
            }

            headers.Add(KafkaConstants.HeaderConstants.RetryExceptionMessage, Encoding.UTF8.GetBytes(message + Environment.NewLine + stackTrace));

            // Retry Count
            if (headers.TryGetLastBytes(KafkaConstants.HeaderConstants.RetryCount, out var retryValue))
            {
                // Parse the current value, increment, and update the header
                var retryCountString = Encoding.UTF8.GetString(retryValue);
                if (!int.TryParse(retryCountString, out var retryCount))
                {
                    retryCount = 0;
                }
                retryCount++;
                headers.Remove(KafkaConstants.HeaderConstants.RetryCount);
                headers.Add(KafkaConstants.HeaderConstants.RetryCount, Encoding.UTF8.GetBytes(retryCount.ToString()));
            }
            else
            {
                headers.Add(KafkaConstants.HeaderConstants.RetryCount, Encoding.UTF8.GetBytes(1.ToString()));
            }

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