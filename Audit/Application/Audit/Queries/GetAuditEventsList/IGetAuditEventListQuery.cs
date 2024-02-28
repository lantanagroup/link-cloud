using LantanaGroup.Link.Audit.Application.Models;

namespace LantanaGroup.Link.Audit.Application.Audit.Queries
{
    public interface IGetAuditEventListQuery
    {
        Task<PagedAuditModel> Execute(AuditSearchFilterRecord searchFilter);
    }
}
