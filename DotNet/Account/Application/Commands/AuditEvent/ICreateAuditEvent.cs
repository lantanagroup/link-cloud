using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Account.Application.Commands.AuditEvent
{
    public interface ICreateAuditEvent
    {
        Task<bool> Execute(AuditEventMessage model, CancellationToken cancellationToken = default);
    }
}
