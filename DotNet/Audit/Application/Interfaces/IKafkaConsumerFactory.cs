using Confluent.Kafka;
using LantanaGroup.Link.Audit.Application.Models;

namespace LantanaGroup.Link.Audit.Application.Interfaces
{
    public interface IKafkaConsumerFactory
    {
        public IConsumer<string, AuditEventMessage> CreateAuditableEventConsumer(bool enableAutoCommit);
    }
}
