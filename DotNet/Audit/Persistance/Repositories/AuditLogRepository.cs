using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Domain.Entities;
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
                await _dbContext.AuditLogs.AsNoTracking().FirstOrDefaultAsync(x => x.AuditId.Equals(id), cancellationToken) :
                await _dbContext.AuditLogs.FindAsync([id, cancellationToken], cancellationToken: cancellationToken);

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
