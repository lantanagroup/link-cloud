using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Audit.Application.Interfaces
{
    public interface IAuditEventProcessor
    {
        Task<bool> ProcessAuditEvent(ConsumeResult<string, AuditEventMessage>? result, CancellationToken cancellationToken);
    }
}
