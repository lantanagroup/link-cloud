using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Audit.Application.Handlers.Exceptions.DeadLetter
{
    public class AuditDeadLetterExceptionHandler<K, V> : DeadLetterExceptionHandler<K, V>
    {
        public AuditDeadLetterExceptionHandler(ILogger<AuditDeadLetterExceptionHandler<K, V>> logger,
            IKafkaProducerFactory<K, V> producerFactory,
            IKafkaProducerFactory<string, string> stringProducerFactory,
            IKafkaProducerFactory<string, AuditEventMessage> auditProducerFactory) 
            : base (logger, auditProducerFactory, producerFactory, stringProducerFactory)
        {

        }

        public override void ProduceAuditEvent(AuditEventMessage auditValue, Headers headers)
        {
            return;
        }
    }
}
