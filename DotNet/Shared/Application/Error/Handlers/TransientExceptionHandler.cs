using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.Extensions.Logging;
using System.Text;

namespace LantanaGroup.Link.Shared.Application.Error.Handlers
{
    public class TransientExceptionHandler<K, V> : ITransientExceptionHandler<K, V>
    {
        protected readonly ILogger<TransientExceptionHandler<K, V>> Logger;
        protected readonly IKafkaProducerFactory<string, AuditEventMessage> AuditProducerFactory;
        protected readonly IKafkaProducerFactory<K, V> ProducerFactory;

        public string Topic { get; set; } = string.Empty;

        public string ServiceName { get; set; } = string.Empty;

        public TransientExceptionHandler(ILogger<TransientExceptionHandler<K, V>> logger,
            IKafkaProducerFactory<string, AuditEventMessage> auditProducerFactory,
            IKafkaProducerFactory<K, V> producerFactory)
        {
            Logger = logger;
            AuditProducerFactory = auditProducerFactory;
            ProducerFactory = producerFactory;
        }

        public void HandleException(ConsumeResult<K, V> consumeResult, string facilityId, AuditEventType auditEventType, string message = "")
        {
            try
            {
                message = message ?? "";
                if (consumeResult == null)
                {
                    Logger.LogError($"{GetType().Name}|{ServiceName}|{Topic}: consumeResult is null, cannot produce Audit or Retry events: " + message);
                    return;
                }

                Logger.LogError($"{GetType().Name}: Failed to process {ServiceName} Event: " + message);

                var auditValue = new AuditEventMessage
                {
                    FacilityId = facilityId,
                    Action = auditEventType,
                    ServiceName = ServiceName,
                    EventDate = DateTime.UtcNow,
                    Notes = $"{GetType().Name}: processing failure in {ServiceName} \nException Message: {message}",
                };

                ProduceAuditEvent(auditValue, consumeResult.Message.Headers);

                if (consumeResult.Message.Headers == null)
                    consumeResult.Message.Headers = new Headers();

                if (!consumeResult.Message.Headers.Any(h => h.Key == message))
                {
                    consumeResult.Message.Headers.Add(new Header(message, Encoding.UTF8.GetBytes(string.Empty)));
                }

                ProduceRetryScheduledEvent(consumeResult.Message.Key, consumeResult.Message.Value,
                    consumeResult.Message.Headers, facilityId);
            }
            catch (Exception e)
            {
                Logger.LogError(e, $"Error in {GetType().Name}.HandleException: " + e.Message);
                throw;
            }
        }

        public virtual void HandleException(ConsumeResult<K, V> consumeResult, Exception ex, AuditEventType auditEventType, string facilityId)
        {
            var tEx = new TransientException(ex.Message, auditEventType, ex.InnerException);
            HandleException(consumeResult, tEx, facilityId);
        }

        public virtual void HandleException(ConsumeResult<K, V>? consumeResult, TransientException ex, string facilityId)
        {
            try
            {
                if (consumeResult == null)
                {
                    Logger.LogError(message: $"{GetType().Name}|{ServiceName}|{Topic}: consumeResult is null, cannot produce Audit or Retry events", exception: ex);
                    return;
                }

                Logger.LogError(message: $"{GetType().Name}: Failed to process {ServiceName} Event.", exception: ex);

                var auditValue = new AuditEventMessage
                {
                    FacilityId = facilityId,
                    Action = ex.AuditEventType,
                    ServiceName = ServiceName,
                    EventDate = DateTime.UtcNow,
                    Notes = $"{GetType().Name}: processing failure in {ServiceName} \nException Message: {ex.Message}",
                };

                ProduceAuditEvent(auditValue, consumeResult.Message.Headers);

                if (consumeResult.Message.Headers == null)
                    consumeResult.Message.Headers = new Headers();

                if (!consumeResult.Message.Headers.Any(h => h.Key == ex.Message))
                {
                    consumeResult.Message.Headers.Add(new Header(ex.Message, Encoding.UTF8.GetBytes(ex?.StackTrace ?? string.Empty)));
                }

                ProduceRetryScheduledEvent(consumeResult.Message.Key, consumeResult.Message.Value,
                    consumeResult.Message.Headers, facilityId);
            }
            catch (Exception e)
            {
                Logger.LogError(e, $"Error in {GetType().Name}.HandleException: " + e.Message);
                throw;
            }
        }

        public virtual void ProduceAuditEvent(AuditEventMessage auditValue, Headers headers)
        {
            using var producer = AuditProducerFactory.CreateAuditEventProducer(useOpenTelemetry:false);
            producer.Produce(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
            {
                Value = auditValue,
                Headers = headers
            });
            producer.Flush();
        }

        public virtual void ProduceRetryScheduledEvent(K key, V value, Headers headers, string facilityId)
        {
            if (string.IsNullOrWhiteSpace(Topic))
            {
                throw new Exception(
                    $"{GetType().Name}.Topic has not been configured. Cannot Produce Retry Event for {ServiceName}");
            }

            if (!headers.TryGetLastBytes(KafkaConstants.HeaderConstants.ExceptionService, out var headerValue))
            {
                headers.Add(KafkaConstants.HeaderConstants.ExceptionService, Encoding.UTF8.GetBytes(ServiceName));
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