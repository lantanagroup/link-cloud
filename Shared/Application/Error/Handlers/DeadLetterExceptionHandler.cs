using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.Extensions.Logging;
using System.Text;

namespace LantanaGroup.Link.Shared.Application.Error.Handlers
{
    public class DeadLetterExceptionHandler<K, V> : IConsumerExceptionHandler<K, V>
    {
        protected readonly ILogger<DeadLetterExceptionHandler<K, V>> Logger;
        protected readonly IKafkaProducerFactory<string, AuditEventMessage> AuditProducerFactory;
        protected readonly IKafkaProducerFactory<K, V> ProducerFactory;

        /// <summary>
        /// The Topic to use when publishing Retry Kafka events.
        /// </summary>
        public string Topic { get; set; } = string.Empty;

        /// <summary>
        /// The name of the service that is consuming the ExceptionHandler.
        /// </summary>
        public string ServiceName { get; set; } = string.Empty;

        public DeadLetterExceptionHandler(ILogger<DeadLetterExceptionHandler<K, V>> logger, 
            IKafkaProducerFactory<string, AuditEventMessage> auditProducerFactory,
            IKafkaProducerFactory<K, V> producerFactory)
        {
            Logger = logger;
            AuditProducerFactory = auditProducerFactory;
            ProducerFactory = producerFactory;
        }

        public virtual void HandleException(ConsumeResult<K, V> consumeResult, Exception ex)
        {
            try
            {
                Logger.LogError(message: $"Failed to process {ServiceName} Event.", exception: ex);

                var auditValue = new AuditEventMessage
                {
                    FacilityId = consumeResult.Message.Key as string,
                    Action = AuditEventType.Query,
                    ServiceName = ServiceName,
                    EventDate = DateTime.UtcNow,
                    Notes = $"{ServiceName} processing failure \nException Message: {ex.Message}",
                };

                ProduceAuditEvent(auditValue, consumeResult.Message.Headers);

                consumeResult.Message.Headers.Add("X-Exception-Message", Encoding.UTF8.GetBytes(ex.Message));
                ProduceEvent(consumeResult.Message.Key, consumeResult.Message.Value, consumeResult.Message.Headers);
            }
            catch (Exception e)
            {
                Logger.LogError(exception: e, message: "Error in ExceptionHandler.HandleException: " + e.Message);
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

        public virtual void ProduceEvent(K key, V value, Headers headers)
        {
            if (string.IsNullOrWhiteSpace(Topic))
            {
                throw new Exception(
                    "DeadLetterExceptionHandler.Topic has not been configured. Cannot Produce Scheduled Event");
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