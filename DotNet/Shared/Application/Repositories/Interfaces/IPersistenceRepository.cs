namespace LantanaGroup.Link.Shared.Application.Repositories.Interfaces;

public interface IPersistenceRepository<T>
{
    public T Add(T entity);
    public Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    public T Get(string id);
    public Task<T> GetAsync(string id, CancellationToken cancellationToken = default);
    public T Update(T entity);
    public Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default);
    public void Delete(string id);
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
