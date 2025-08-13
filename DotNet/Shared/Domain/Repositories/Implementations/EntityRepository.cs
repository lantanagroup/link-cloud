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
        protected readonly DbContext _dbContext;

        public EntityRepository(DbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public Task<T> AddAsync(T entity)
        {
            return AddAsync(entity, CancellationToken.None);
        }

        public async Task<T> AddAsync(T entity, CancellationToken cancellationToken)
        {
            var result = (await _dbContext.Set<T>().AddAsync(entity, cancellationToken)).Entity;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return result;

        }

        public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
        {
            return AnyAsync(predicate, CancellationToken.None);
        }

        public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken)
        {
            return await _dbContext.Set<T>().AnyAsync(predicate, cancellationToken);
        }

        public Task CommitTransactionAsync()
        {
            return CommitTransactionAsync(CancellationToken.None);
        }

        public async Task CommitTransactionAsync(CancellationToken cancellationToken)
        {
            await _dbContext.Database.CommitTransactionAsync(cancellationToken);
        }

        public Task DeleteAsync(T entity)
        {
            return DeleteAsync(entity, CancellationToken.None);
        }

        public async Task DeleteAsync(T entity, CancellationToken cancellationToken)
        {
            _dbContext.Set<T>().Remove(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public void Remove(T entity)
        {
            _dbContext.Set<T>().Remove(entity);
            _dbContext.SaveChanges();
        }

        public Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            return FindAsync(predicate, CancellationToken.None);
        }

        public async Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken)
        {
            IQueryable<T> source = _dbContext.Set<T>().AsQueryable();
            if (predicate != null)
            {
                source = source.Where(predicate);
            }

            return await source.ToListAsync(cancellationToken);
        }

        public Task<T> FirstAsync(Expression<Func<T, bool>> predicate)
        {
            return FirstAsync(predicate, CancellationToken.None);
        }

        public async Task<T> FirstAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken)
        {
            return await _dbContext.Set<T>().FirstAsync(predicate, cancellationToken);
        }

        public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            return FirstOrDefaultAsync(predicate, CancellationToken.None);
        }

        public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken)
        {
            return await _dbContext.Set<T>().FirstOrDefaultAsync(predicate, cancellationToken);
        }

        public Task<List<T>> GetAllAsync()
        {
            return GetAllAsync(CancellationToken.None);
        }

        public async Task<List<T>> GetAllAsync(CancellationToken cancellationToken)
        {
            return await _dbContext.Set<T>().ToListAsync(cancellationToken);
        }

        public Task<T> GetAsync(object id)
        {
            return GetAsync(id, CancellationToken.None);
        }

        public async Task<T> GetAsync(object id, CancellationToken cancellationToken)
        {
            return await _dbContext.Set<T>().FindAsync(new[] { id }, cancellationToken);
        }

        public Task<HealthCheckResult> HealthCheck(int eventId)
        {
            return HealthCheck(eventId, CancellationToken.None);
        }

        public async Task<HealthCheckResult> HealthCheck(int eventId, CancellationToken cancellationToken)
        {
            try
            {
                bool outcome = await _dbContext.Database.CanConnectAsync(cancellationToken);

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

        public Task RollbackTransactionAsync()
        {
            return RollbackTransactionAsync(CancellationToken.None);
        }

        public async Task RollbackTransactionAsync(CancellationToken cancellationToken)
        {
            await _dbContext.Database.RollbackTransactionAsync(cancellationToken);
        }

        public Task SaveChangesAsync()
        {
            return SaveChangesAsync(CancellationToken.None);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public Task<(List<T>, PaginationMetadata)> SearchAsync(Expression<Func<T, bool>> predicate, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber)
        {
            return SearchAsync(predicate, sortBy, sortOrder, pageSize, pageNumber, CancellationToken.None);
        }

        public async Task<(List<T>, PaginationMetadata)> SearchAsync(Expression<Func<T, bool>> predicate, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken)
        {
            var query = _dbContext.Set<T>().AsNoTracking().AsQueryable();

            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            var count = await query.CountAsync(cancellationToken);

            if (sortOrder != null)
            {
                query = sortOrder switch
                {
                    SortOrder.Ascending => query.OrderBy(SetSortBy<T>(sortBy)),
                    SortOrder.Descending => query.OrderByDescending(SetSortBy<T>(sortBy)),
                    _ => query
                };
            }

            var results = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            PaginationMetadata metadata = new PaginationMetadata(pageSize, pageNumber, count);

            return (results, metadata);
        }

        private Expression<Func<T, object>> SetSortBy<T>(string? sortBy)
        {
            var sortKey = sortBy?.ToLower() ?? "";
            var parameter = Expression.Parameter(typeof(T), "p");
            var sortExpression = Expression.Lambda<Func<T, object>>(Expression.Convert(Expression.Property(parameter, sortKey), typeof(object)), parameter);

            return sortExpression;
        }

        public Task<T> SingleAsync(Expression<Func<T, bool>> predicate)
        {
            return SingleAsync(predicate, CancellationToken.None);
        }

        public async Task<T> SingleAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken)
        {
            return await _dbContext.Set<T>().SingleAsync(predicate, cancellationToken);
        }

        public Task<T?> SingleOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            return SingleOrDefaultAsync(predicate, CancellationToken.None);
        }

        public async Task<T?> SingleOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken)
        {
            return await _dbContext.Set<T>().SingleOrDefaultAsync(predicate, cancellationToken);
        }

        public Task StartTransactionAsync()
        {
            return StartTransactionAsync(CancellationToken.None);
        }

        public async Task StartTransactionAsync(CancellationToken cancellationToken)
        {
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        }

        public Task UpdateAsync(T entity)
        {
            return UpdateAsync(entity, CancellationToken.None);
        }

        public async Task UpdateAsync(T entity, CancellationToken cancellationToken)
        {
            _dbContext.Set<T>().Update(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}