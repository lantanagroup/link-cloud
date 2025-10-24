using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Linq.Expressions;
using System.Reflection;

namespace LantanaGroup.Link.Shared.Domain.Repositories.Implementations
{
    public class EntityRepository<T, TDbContext> : IEntityRepository<T> where T : class where TDbContext : DbContext
    {
        protected readonly TDbContext _dbContext;

        public EntityRepository(TDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        // CRUD Operations
        public Task<T> AddAsync(T entity)
        {
            return AddAsync(entity, CancellationToken.None);
        }

        public async Task<T> AddAsync(T entity, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = (await _dbContext.Set<T>().AddAsync(entity, cancellationToken)).Entity;
            return result;
        }

        public Task<T> GetAsync(object id)
        {
            return GetAsync(id, CancellationToken.None);
        }

        public async Task<T> GetAsync(object id, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _dbContext.Set<T>().FindAsync(new[] { id }, cancellationToken).ConfigureAwait(false);
        }

        public void Update(T entity)
        {
            _dbContext.Set<T>().Update(entity);
        }

        public void Remove(T entity)
        {
            _dbContext.Set<T>().Remove(entity);
        }

        // Query Methods
        public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
        {
            return AnyAsync(predicate, CancellationToken.None);
        }

        public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _dbContext.Set<T>().AnyAsync(predicate, cancellationToken);
        }

        public Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            return FindAsync(predicate, CancellationToken.None);
        }

        public async Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
            cancellationToken.ThrowIfCancellationRequested();
            return await _dbContext.Set<T>().FirstAsync(predicate, cancellationToken);
        }

        public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            return FirstOrDefaultAsync(predicate, CancellationToken.None);
        }

        public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _dbContext.Set<T>().FirstOrDefaultAsync(predicate, cancellationToken);
        }

        public Task<List<T>> GetAllAsync()
        {
            return GetAllAsync(CancellationToken.None);
        }

        public async Task<List<T>> GetAllAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _dbContext.Set<T>().ToListAsync(cancellationToken);
        }

        public Task<T> SingleAsync(Expression<Func<T, bool>> predicate)
        {
            return SingleAsync(predicate, CancellationToken.None);
        }

        public async Task<T> SingleAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _dbContext.Set<T>().SingleAsync(predicate, cancellationToken);
        }

        public Task<T?> SingleOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            return SingleOrDefaultAsync(predicate, CancellationToken.None);
        }

        public async Task<T?> SingleOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _dbContext.Set<T>().SingleOrDefaultAsync(predicate, cancellationToken);
        }

        // Search and Pagination
        public Task<(List<T>, PaginationMetadata)> SearchAsync(Expression<Func<T, bool>> predicate, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber)
        {
            return SearchAsync(predicate, sortBy, sortOrder, pageSize, pageNumber, true, CancellationToken.None);
        }

        public Task<(List<T>, PaginationMetadata)> SearchAsync(Expression<Func<T, bool>> predicate, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken)
        {
            return SearchAsync(predicate, sortBy, sortOrder, pageSize, pageNumber, true, cancellationToken);
        }

        public async Task<(List<T>, PaginationMetadata)> SearchAsync(Expression<Func<T, bool>> predicate, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, bool asNoTracking, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var query = asNoTracking ? _dbContext.Set<T>().AsNoTracking().AsQueryable() : _dbContext.Set<T>().AsQueryable();

            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            var count = await query.CountAsync(cancellationToken);

            sortOrder ??= SortOrder.Descending;
            sortBy ??= "Id";

            var sortExpression = SetSortBy(sortBy);
            query = sortOrder switch
            {
                SortOrder.Ascending => query.OrderBy(sortExpression),
                SortOrder.Descending => query.OrderByDescending(sortExpression),
                _ => query
            };
            

            var results = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            PaginationMetadata metadata = new PaginationMetadata(pageSize, pageNumber, count);

            return (results, metadata);
        }

        // Transaction Management
        public Task StartTransactionAsync()
        {
            return StartTransactionAsync(CancellationToken.None);
        }

        public async Task StartTransactionAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        }

        public Task CommitTransactionAsync()
        {
            return CommitTransactionAsync(CancellationToken.None);
        }

        public async Task CommitTransactionAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _dbContext.Database.CommitTransactionAsync(cancellationToken);
        }

        public Task RollbackTransactionAsync()
        {
            return RollbackTransactionAsync(CancellationToken.None);
        }

        public async Task RollbackTransactionAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _dbContext.Database.RollbackTransactionAsync(cancellationToken);
        }

        public Task SaveChangesAsync()
        {
            return SaveChangesAsync(CancellationToken.None);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // Health Check
        public Task<HealthCheckResult> HealthCheck(int eventId)
        {
            return HealthCheck(eventId, CancellationToken.None);
        }

        public async Task<HealthCheckResult> HealthCheck(int eventId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
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

        // Private Helper Methods
        private Expression<Func<T, object>> SetSortBy(string? sortBy)
        {
            if (string.IsNullOrWhiteSpace(sortBy))
            {
                throw new ArgumentException("sortBy cannot be null or empty.");
            }

            var propertyInfo = typeof(T).GetProperty(sortBy, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (propertyInfo == null)
            {
                throw new ArgumentException($"Property '{sortBy}' does not exist on type '{typeof(T).Name}'.");
            }

            var parameter = Expression.Parameter(typeof(T), "p");
            var property = Expression.Property(parameter, propertyInfo);
            return Expression.Lambda<Func<T, object>>(Expression.Convert(property, typeof(object)), parameter);
        }
    }
}