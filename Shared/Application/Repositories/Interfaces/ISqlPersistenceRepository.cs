
namespace LantanaGroup.Link.Shared.Application.Repositories.Interfaces
{
    public interface ISqlPersistenceRepository<T> where T : class
    {
        public Task<List<T>> GetAsync(CancellationToken cancellationToken);
        public Task<T> GetAsyncById(Guid id, CancellationToken cancellationToken);
        public Task<bool> CreateAsync(T entity, CancellationToken cancellationToken);
        public Task<bool> UpdateAsync(T entity, CancellationToken cancellationToken);
        public Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken);

    }
}
