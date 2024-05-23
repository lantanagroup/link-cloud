using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Audit.Application.Handlers.Exceptions.Transient
{
    public class AuditTransientExceptionHandler<K, V> : TransientExceptionHandler<K, V>
    {
        public AuditTransientExceptionHandler(ILogger<AuditTransientExceptionHandler<K, V>> logger,
            IKafkaProducerFactory<string, AuditEventMessage> auditProducerFactory,
            IKafkaProducerFactory<K, V> producerFactory) : base(logger, auditProducerFactory, producerFactory) { }

        public override void ProduceAuditEvent(AuditEventMessage auditValue, Headers headers)
        {
            return;
        }
    }
}
