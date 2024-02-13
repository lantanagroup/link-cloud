using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Domain.Entities;

namespace LantanaGroup.Link.Audit.Persistance.Repositories
{
    public class AuditLogRepository : IAuditRepository
    {
        private readonly ILogger<AuditLogRepository> _logger;

        public AuditLogRepository(ILogger<AuditLogRepository> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<bool> Add(AuditLog entity)
        {
            throw new NotImplementedException();
        }

        public Task<AuditLog> Get(AuditId id)
        {
            throw new NotImplementedException();
        }

        public Task<(IEnumerable<AuditLog>, PaginationMetadata)> GetByFacility(string facilityId, int pageSize, int pageNumber)
        {
            throw new NotImplementedException();
        }

        public Task<(IEnumerable<AuditLog>, PaginationMetadata)> Search(string? searchText, string? filterFacilityBy, string? filterCorrelationBy, string? filterServiceBy, string? filterActionBy, string? filterUserBy, string? sortBy, int pageSize, int pageNumber)
        {
            throw new NotImplementedException();
        }
    }
}
