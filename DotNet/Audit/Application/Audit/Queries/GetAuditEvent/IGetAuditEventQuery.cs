using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Domain.Entities;

namespace LantanaGroup.Link.Audit.Application.Audit.Queries
{
    public interface IGetAuditEventQuery
    {
        Task<AuditModel> Execute(AuditId id, CancellationToken cancellationToken = default);
    }
}
