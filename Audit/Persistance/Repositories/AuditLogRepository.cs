using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Domain.Entities;
using LantanaGroup.Link.Audit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

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

        public async Task<bool> AddAsync(AuditLog entity, CancellationToken cancellationToken = default)
        {
            await _dbContext.AuditLogs.AddAsync(entity, cancellationToken);            
            return await _dbContext.SaveChangesAsync(cancellationToken) > 0;
        }
        
        public async Task<AuditLog?> GetAsync(AuditId id, bool noTracking = false, CancellationToken cancellationToken = default)
        {
            var log = noTracking ?
                await _dbContext.AuditLogs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken) :
                await _dbContext.AuditLogs.FindAsync(id, cancellationToken);

            return log;
        }

        public async Task<(IEnumerable<AuditLog>, PaginationMetadata)> GetByFacilityAsync(string facilityId, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken = default)
        {
            IEnumerable<AuditLog> logs;
            var query = _dbContext.AuditLogs.AsNoTracking().AsQueryable().Where(x => x.FacilityId == facilityId);

            query = sortOrder switch
            {
                SortOrder.Ascending => query.OrderBy(SetSortBy<AuditLog>(sortBy)),
                SortOrder.Descending => query.OrderByDescending(SetSortBy<AuditLog>(sortBy)),
                _ => query.OrderBy(x => x.CreatedOn)
            };

            logs = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var count = await _dbContext.Set<AuditLog>().CountAsync(x => x.FacilityId == facilityId, cancellationToken);
            PaginationMetadata metadata = new PaginationMetadata(pageSize, pageNumber, count);

            var result = (logs, metadata);

            return result;
        }

        public async Task<(IEnumerable<AuditLog>, PaginationMetadata)> SearchAsync(string? searchText, string? filterFacilityBy, string? filterCorrelationBy, string? filterServiceBy, string? filterActionBy, string? filterUserBy, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken = default)
        {            
            IEnumerable<AuditLog> logs;
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

            #endregion

            query = sortOrder switch
            {
                SortOrder.Ascending => query.OrderBy(SetSortBy<AuditLog>(sortBy)),
                SortOrder.Descending => query.OrderByDescending(SetSortBy<AuditLog>(sortBy)),
                _ => query.OrderBy(x => x.CreatedOn)
            };

            using (ServiceActivitySource.Instance.StartActivity("Get filtered search result"))
            {
                logs = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);
            }

            // get count of all records
            int count = 0;
            using (ServiceActivitySource.Instance.StartActivity("Get total count"))
            {
                count = await query.CountAsync(cancellationToken);
            }
            PaginationMetadata metadata = new PaginationMetadata(pageSize, pageNumber, count);

            var result = (logs, metadata);

            return result;
        }

        /// <summary>
        /// Creates a sort expression for the given sortBy parameter
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sortBy"></param>
        /// <returns></returns>
        private Expression<Func<T, object>> SetSortBy<T>(string? sortBy)
        {
            var sortKey = sortBy switch
            {
                "FacilityId" => "FacilityId",
                "Action" => "Action",
                "ServiceName" => "ServiceName",
                "Resource" => "Resource",
                "CreatedOn" => "CreatedOn",
                _ => "CreatedOn"
            };

            var parameter = Expression.Parameter(typeof(T), "p");
            var sortExpression = Expression.Lambda<Func<T, object>>(Expression.Convert(Expression.Property(parameter, sortKey), typeof(object)), parameter);

            return sortExpression;
        }
    }
}
