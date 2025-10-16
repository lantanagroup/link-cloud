using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Linq.Expressions;

namespace LantanaGroup.Link.Shared.Domain.Repositories.Implementations
{
    public class EntityRepository<T, TDbContext> : IEntityRepository<T> where T : class where TDbContext : DbContext
    {
        protected readonly TDbContext _dbContext;

        public EntityRepository(TDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<T> AddAsync(T entity)
        {
            return (await _dbContext.Set<T>().AddAsync(entity)).Entity;
        }

        public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
        {
           return await _dbContext.Set<T>().AnyAsync(predicate);
        }

        public async Task CommitTransactionAsync()
        {
            await _dbContext.Database.CommitTransactionAsync();
        }

        public void Remove(T entity)
        {
            _dbContext.Set<T>().Remove(entity);
        }

        public async Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            IQueryable<T> source = _dbContext.Set<T>().AsQueryable();
            if(predicate != null)
            {
                source = source.Where(predicate);
            }

            return await source.ToListAsync();
        }

        public async Task<T> FirstAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbContext.Set<T>().FirstAsync(predicate);
        }

        public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbContext.Set<T>().FirstOrDefaultAsync(predicate);
        }

        public async Task<List<T>> GetAllAsync()
        {
            return await _dbContext.Set<T>().ToListAsync();
        }

        public async Task<T> GetAsync(object id)
        {
            return await _dbContext.Set<T>().FindAsync(id);
        }

        public async Task<HealthCheckResult> HealthCheck(int eventId)
        {
            try
            {
                bool outcome = await _dbContext.Database.CanConnectAsync();

                if (outcome)
                {
                    return HealthCheckResult.Healthy();
                }
                else
                {
                    return HealthCheckResult.Unhealthy();
                }

            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy(exception: ex);
            }
        }

        public async Task RollbackTransactionAsync()
        {
            await _dbContext.Database.RollbackTransactionAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _dbContext.SaveChangesAsync();
        }

        public async Task<(List<T>, PaginationMetadata)> SearchAsync(Expression<Func<T, bool>> predicate, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber)
        {

            var query = _dbContext.Set<T>().AsNoTracking().AsQueryable();

            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            var count = await query.CountAsync();

            if (sortOrder != null)
            {
                query = sortOrder switch
                {
                    SortOrder.Ascending => query.OrderBy(SetSortBy<T>(sortBy)),
                    SortOrder.Descending => query.OrderByDescending(SetSortBy<T>(sortBy))
                };
            }

            var results = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            PaginationMetadata metadata = new PaginationMetadata(pageSize, pageNumber, count);

            var result = (results, metadata);

            return result;
        }

        private Expression<Func<T, object>> SetSortBy<T>(string? sortBy)
        {
            var sortKey = sortBy?.ToLower() ?? "";
            var parameter = Expression.Parameter(typeof(T), "p");
            var sortExpression = Expression.Lambda<Func<T, object>>(Expression.Convert(Expression.Property(parameter, sortKey), typeof(object)), parameter);

            return sortExpression;
        }

        public async Task<T> SingleAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbContext.Set<T>().SingleAsync(predicate);
        }

        public async Task<T?> SingleOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbContext.Set<T>().SingleOrDefaultAsync(predicate);
        }

        public async Task StartTransactionAsync()
        {
            await _dbContext.Database.BeginTransactionAsync();
        }
    }
}
