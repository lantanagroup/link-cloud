using Confluent.Kafka;
using LantanaGroup.Link.Notification.Application.Models;

namespace LantanaGroup.Link.Notification.Application.Interfaces
{
    public interface IKafkaProducerFactory
    {
        public IProducer<string, AuditEventMessage> CreateAuditEventProducer(bool useOpenTelemetry);
    }
}
