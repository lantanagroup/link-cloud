namespace LantanaGroup.Link.Shared.Application.Repositories.Interfaces;

public interface IPersistenceRepository<T>
{
    T Add(T entity);
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    T Get(string id);
    Task<T?> GetAsync(string id, CancellationToken cancellationToken = default);
    T Update(T entity);
    Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default);
    void Delete(string id);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
