using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Linq.Expressions;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Shared.Application.Repositories.Interfaces;

public interface IEntityRepository<T>
{
    T Add(T entity);
    Task RemoveAsync(T entity);

    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    T Get(string id);
    Task<T> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<T> FirstAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<T?> SingleOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<T> SingleAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    T Update(T entity);
    Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default);
    void Delete(string id);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<HealthCheckResult> HealthCheck(int eventId);

}
