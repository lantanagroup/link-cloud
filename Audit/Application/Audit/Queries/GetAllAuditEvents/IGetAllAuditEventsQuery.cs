using LantanaGroup.Link.Audit.Application.Models;

namespace LantanaGroup.Link.Audit.Application.Audit.Queries
{
    public interface IGetAllAuditEventsQuery
    {
        Task <List<AuditModel>> Execute();
    }
}
