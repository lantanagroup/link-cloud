using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Domain.Entities;

namespace LantanaGroup.Link.Audit.Application.Interfaces
{
    public interface IAuditRepository : ISearchRepository
    {
        Task<bool> Add(AuditLog entity);
        Task<AuditLog?> Get(AuditId id);
        Task<(IEnumerable<AuditLog>, PaginationMetadata)> GetByFacility(string facilityId, int pageSize, int pageNumber);
    }
}
