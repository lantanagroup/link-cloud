
namespace LantanaGroup.Link.Shared.Application.Repositories.Interfaces
{
    public interface ISqlPersistenceRepository<T> where T : class
    {
        public Task<List<T>> GetAsync(CancellationToken cancellationToken);
        public Task<T> GetAsyncById(string id, CancellationToken cancellationToken);
        public Task<bool> CreateAsync(T entity, CancellationToken cancellationToken);
        public Task<bool> UpdateAsync(T entity, CancellationToken cancellationToken);
        public Task<bool> RemoveAsync(string id, CancellationToken cancellationToken);

    }
}
