using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Domain.Entities;
using LantanaGroup.Link.Audit.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Audit.Persistance.Repositories
{
    public class AuditLogRepository : IAuditRepository
    {
        private readonly ILogger<AuditLogRepository> _logger;
        private readonly AuditDbContext _dbContext;

        public AuditLogRepository(ILogger<AuditLogRepository> logger, AuditDbContext dbContext)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public Task<bool> Add(AuditLog entity)
        {
            _dbContext.AuditLogs.Add(entity);            
            return Task.FromResult(_dbContext.SaveChanges() > 0);
        }
        
        public Task<AuditLog?> Get(AuditId id, bool noTracking = false)
        {
            var log = noTracking ?
                _dbContext.AuditLogs.AsNoTracking().FirstOrDefault(x => x.Id == id) :
                _dbContext.AuditLogs.Find(id);
            
            return Task.FromResult(log);
        }

        public Task<(IEnumerable<AuditLog>, PaginationMetadata)> GetByFacility(string facilityId, int pageSize, int pageNumber)
        {
            var logs = _dbContext.AuditLogs.AsNoTracking().Where(x => x.FacilityId == facilityId)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var count = _dbContext.Set<AuditLog>().Count(x => x.FacilityId == facilityId);
            PaginationMetadata metadata = new PaginationMetadata(pageSize, pageNumber, count);

            return Task.FromResult<(IEnumerable<AuditLog>, PaginationMetadata)>((logs, metadata));
        }

        public Task<(IEnumerable<AuditLog>, PaginationMetadata)> Search(string? searchText, string? filterFacilityBy, string? filterCorrelationBy, string? filterServiceBy, string? filterActionBy, string? filterUserBy, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber)
        {            
            IEnumerable<AuditLog?> logs;
            var query = _dbContext.AuditLogs.AsNoTracking().AsQueryable();

            #region Build Query
            if (searchText is not null && searchText.Length > 0)
            {
                query = query.Where(x =>
                    (x.CorrelationId != null && x.CorrelationId.Contains(searchText)) ||
                    (x.Resource != null && x.Resource.Contains(searchText)) ||
                    (x.User != null && x.User.Contains(searchText)) ||
                    (x.Notes != null && x.Notes.Contains(searchText)) ||
                    (x.PropertyChanges != null && x.PropertyChanges.Any(y => y.NewPropertyValue.Contains(searchText)))
                );
            }

            if (filterFacilityBy is not null && filterFacilityBy.Length > 0)
            {
                query = query.Where(x => x.FacilityId == filterFacilityBy);
            }

            if (filterCorrelationBy is not null && filterCorrelationBy.Length > 0)
            {
                query = query.Where(x => x.CorrelationId == filterCorrelationBy);
            }

            if (filterServiceBy is not null && filterServiceBy.Length > 0)
            {
                query = query.Where(x => x.ServiceName == filterServiceBy);
            }

            if (filterActionBy is not null && filterActionBy.Length > 0)
            {
                query = query.Where(x => x.Action == filterActionBy);
            }

            if (filterUserBy is not null && filterUserBy.Length > 0)
            {
                query = query.Where(x => x.User == filterUserBy);
            }

            if (sortBy is not null && sortBy.Length > 0)
            {
                query = sortBy switch
                {
                    "Facility" => query.OrderBy(x => x.FacilityId),
                    "Correlation" => query.OrderBy(x => x.CorrelationId),
                    "Service" => query.OrderBy(x => x.ServiceName),
                    "Action" => query.OrderBy(x => x.Action),
                    "User" => query.OrderBy(x => x.User),
                    "Date" => query.OrderBy(x => x.CreatedOn),
                    _ => query.OrderBy(x => x.CreatedOn)
                };
            }

            #endregion

            query = sortOrder switch
            {
                SortOrder.Ascending => query.OrderBy(_dbContext.SetSortBy<AuditLog>(sortBy)),
                SortOrder.Descending => query.OrderByDescending(_dbContext.SetSortBy<AuditLog>(sortBy)),
                _ => query.OrderBy(x => x.CreatedOn)
            };

            using (ServiceActivitySource.Instance.StartActivity("Get filtered search result"))
            {
                logs = query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();
            }

            // get count of all records
            int count = 0;
            using (ServiceActivitySource.Instance.StartActivity("Get total count"))
            {
                count = query.Count();
            }
            PaginationMetadata metadata = new PaginationMetadata(pageSize, pageNumber, count);

            return Task.FromResult<(IEnumerable<AuditLog>, PaginationMetadata)>((logs, metadata));
        }
    }
}
