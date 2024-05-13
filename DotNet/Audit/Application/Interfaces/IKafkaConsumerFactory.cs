using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Audit.Application.Interfaces
{
    public interface IKafkaConsumerFactory
    {
        public IConsumer<string, AuditEventMessage> CreateAuditableEventConsumer(bool enableAutoCommit);
    }
}
