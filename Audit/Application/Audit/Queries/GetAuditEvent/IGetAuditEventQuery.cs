using LantanaGroup.Link.Audit.Application.Models;

namespace LantanaGroup.Link.Audit.Application.Audit.Queries
{
    public interface IGetAuditEventQuery
    {
        Task<AuditModel> Execute(string id);
    }
}
