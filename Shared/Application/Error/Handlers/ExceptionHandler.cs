using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.Shared.Application.Error.Handlers
{
    public class ExceptionHandler<K, V> : IExceptionHandler<K, V>
    {
        protected readonly ILogger<ExceptionHandler<K, V>> Logger;
        protected readonly IKafkaProducerFactory<string, AuditEventMessage> AuditProducerFactory;

        /// <summary>
        /// The name of the service that is consuming the ExceptionHandler.
        /// </summary>
        public string ServiceName { get; set; }

        public ExceptionHandler(ILogger<ExceptionHandler<K, V>> logger, IKafkaProducerFactory<string, AuditEventMessage> auditProducerFactory)
        {
            Logger = logger;
            AuditProducerFactory = auditProducerFactory;
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

                ProduceAuditEvent(AuditProducerFactory, auditValue, consumeResult.Message.Headers);
            }
            catch (Exception e)
            {
                Logger.LogError(exception: e, message: "Error in ExceptionHandler.HandleException: " + e.Message);
            }
        }

        public virtual void ProduceAuditEvent(IKafkaProducerFactory<string, AuditEventMessage> auditProducerFactory, AuditEventMessage auditValue, Headers headers)
        {
            using var producer = auditProducerFactory.CreateAuditEventProducer();
            producer.Produce(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
            {
                Value = auditValue,
                Headers = headers
            });
            producer.Flush();
        }
    }
}