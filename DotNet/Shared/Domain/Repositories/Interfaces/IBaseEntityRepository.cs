using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Linq.Expressions;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;

public interface IBaseEntityRepository<T>
{
    T Add(T entity);
    Task RemoveAsync(T entity);

    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    T Get(object id);
    Task<T> GetAsync(object id, CancellationToken cancellationToken = default);
    Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<T> FirstAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<T?> SingleOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<T> SingleAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    T Update(T entity);
    Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default);
    void Delete(object id);
    Task DeleteAsync(T? entity, CancellationToken cancellationToken);
    Task DeleteAsync(object id, CancellationToken cancellationToken = default);
    Task<(List<T>, PaginationMetadata)> SearchAsync(Expression<Func<T, bool>> predicate, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken = default);
    Task<HealthCheckResult> HealthCheck(int eventId);
    void StartTransaction();
    void CommitTransaction();
    void RollbackTransaction();
    Task StartTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken);
}
