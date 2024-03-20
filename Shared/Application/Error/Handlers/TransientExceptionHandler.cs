using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.Extensions.Logging;

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

        public virtual void HandleException(ConsumeResult<K, V>? consumeResult, Exception ex, string facilityId)
        {
            try
            {
                if (consumeResult == null)
                {
                    Logger.LogError(message: $"TransientExceptionHandler|{ServiceName}|{Topic}: consumeResult is null, cannot produce Audit or Retry events", exception: ex);
                    return;
                }

                Logger.LogError(message: $"TransientExceptionHandler: Failed to process {ServiceName} Event.", exception: ex);

                var auditValue = new AuditEventMessage
                {
                    FacilityId = facilityId,
                    Action = AuditEventType.Query,
                    ServiceName = ServiceName,
                    EventDate = DateTime.UtcNow,
                    Notes = $"TransientExceptionHandler: processing failure in {ServiceName} \nException Message: {ex.Message}",
                };

                ProduceAuditEvent(auditValue, consumeResult.Message.Headers);
                ProduceRetryScheduledEvent(consumeResult.Message.Key, consumeResult.Message.Value,
                    consumeResult.Message.Headers);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error in TransientExceptionHandler.HandleException: " + e.Message);
                throw;
            }
        }

        public virtual void ProduceAuditEvent(AuditEventMessage auditValue, Headers headers)
        {
            using var producer = AuditProducerFactory.CreateAuditEventProducer();
            producer.Produce(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
            {
                Value = auditValue,
                Headers = headers
            });
            producer.Flush();
        }

        public virtual void ProduceRetryScheduledEvent(K key, V value, Headers headers)
        {
            if (string.IsNullOrWhiteSpace(Topic))
            {
                throw new Exception(
                    $"TransientExceptionHandler.Topic has not been configured. Cannot Produce Retry Event for {ServiceName}");
            }

            using var producer = ProducerFactory.CreateProducer(new ProducerConfig());
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