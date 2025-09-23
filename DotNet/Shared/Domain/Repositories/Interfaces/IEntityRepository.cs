using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Linq.Expressions;

namespace LantanaGroup.Link.Shared.Domain.Repositories.Interfaces
{
    public interface IEntityRepository<T>
    {
        Task<T> AddAsync(T entity);
        Task<T> AddAsync(T entity, CancellationToken cancellationToken);
        Task<T> GetAsync(object id);
        Task<T> GetAsync(object id, CancellationToken cancellationToken);
        Task<List<T>> GetAllAsync();
        Task<List<T>> GetAllAsync(CancellationToken cancellationToken);
        Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate);
        Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken);
        Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);
        Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken);
        Task<T> FirstAsync(Expression<Func<T, bool>> predicate);
        Task<T> FirstAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken);
        Task<T?> SingleOrDefaultAsync(Expression<Func<T, bool>> predicate);
        Task<T?> SingleOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken);
        Task<T> SingleAsync(Expression<Func<T, bool>> predicate);
        Task<T> SingleAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken);
        void Remove(T entity);
        Task<(List<T>, PaginationMetadata)> SearchAsync(Expression<Func<T, bool>> predicate, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber);
        Task<(List<T>, PaginationMetadata)> SearchAsync(Expression<Func<T, bool>> predicate, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken);
        Task<HealthCheckResult> HealthCheck(int eventId);
        Task<HealthCheckResult> HealthCheck(int eventId, CancellationToken cancellationToken);
        Task StartTransactionAsync();
        Task StartTransactionAsync(CancellationToken cancellationToken);
        Task CommitTransactionAsync();
        Task CommitTransactionAsync(CancellationToken cancellationToken);
        Task RollbackTransactionAsync();
        Task RollbackTransactionAsync(CancellationToken cancellationToken);
        Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);
        Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken);
        Task SaveChangesAsync();
        Task SaveChangesAsync(CancellationToken cancellationToken);
        Task UpdateAsync(T entity);
        Task UpdateAsync(T entity, CancellationToken cancellationToken);
        Task DeleteAsync(T entity);
        Task DeleteAsync(T entity, CancellationToken cancellationToken);
    }
}