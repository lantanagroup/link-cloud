using Confluent.Kafka;
using LantanaGroup.Link.MeasureEval.Models;

namespace LantanaGroup.Link.MeasureEval.Auditing
{
    public interface IKafkaProducerFactory
    {
        public IProducer<string, AuditEventMessage> CreateAuditEventProducer();
    }
}
