using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Domain.Entities;

namespace LantanaGroup.Link.Audit.Application.Interfaces
{
    public interface IAuditRepository : ISearchRepository
    {
        Task<bool> AddAsync(AuditLog entity, CancellationToken cancellationToken = default);
        Task<AuditLog?> GetAsync(AuditId id, bool noTracking = false, CancellationToken cancellationToken = default);
        Task<(IEnumerable<AuditLog>, PaginationMetadata)> GetByFacilityAsync(string facilityId, int pageSize, int pageNumber, CancellationToken cancellationToken = default);
    }
}
