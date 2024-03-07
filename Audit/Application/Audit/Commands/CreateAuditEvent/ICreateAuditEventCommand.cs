using LantanaGroup.Link.Audit.Domain.Entities;

namespace LantanaGroup.Link.Audit.Application.Commands
{
    public interface ICreateAuditEventCommand
    {
        Task<AuditLog> Execute(CreateAuditEventModel model, CancellationToken cancellationToken = default);
    }
}
