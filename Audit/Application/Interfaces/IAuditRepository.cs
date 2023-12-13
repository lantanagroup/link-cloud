using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Domain.Entities;

namespace LantanaGroup.Link.Audit.Application.Interfaces
{
    public interface IAuditRepository
    {
        bool Add(AuditEntity entity);
        Task<bool> AddAsync(AuditEntity entity);
        AuditEntity Get(string id);
        Task<AuditEntity> GetAsync(string id);
        IEnumerable<AuditEntity> GetAll();
        Task<IEnumerable<AuditEntity>> GetAllAsync();
        (IEnumerable<AuditEntity>, PaginationMetadata) Find(string? searchText, string? filterFacilityBy, string? filterCorrelationBy, string? filterServiceBy, string? filterActionBy, string? filterUserBy, string? sortBy, int pageSize, int pageNumber);
        Task<(IEnumerable<AuditEntity>, PaginationMetadata)> FindAsync(string? searchText, string? filterFacilityBy, string? filterCorrelationBy, string? filterServiceBy, string? filterActionBy, string? filterUserBy, string? sortBy, int pageSize, int pageNumber);
        Task<bool> HealthCheck();

    }
}
