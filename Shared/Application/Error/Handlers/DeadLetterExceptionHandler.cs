using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.Extensions.Logging;
using System.Text;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;

namespace LantanaGroup.Link.Shared.Application.Error.Handlers
{
    public class DeadLetterExceptionHandler<K, V> : IDeadLetterExceptionHandler<K, V>
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
                ProduceDeadLetter(consumeResult.Message.Key, consumeResult.Message.Value, consumeResult.Message.Headers, ex.Message);
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

        public virtual void ProduceDeadLetter(K key, V value, Headers headers, string exceptionMessage)
        {
            if (string.IsNullOrWhiteSpace(Topic))
            {
                throw new Exception(
                    "TransientExceptionHandler.Topic has not been configured. Cannot Produce Scheduled Event");
            }

            headers.Add("X-Exception-Message", Encoding.UTF8.GetBytes(exceptionMessage));

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