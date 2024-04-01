
namespace LantanaGroup.Link.Tenant.Repository.Interfaces.Sql
{
    public interface IPersistenceRepository<T> where T : class
    {
        public Task<List<T>> GetAsync(CancellationToken cancellationToken);
        public Task<T> GetAsyncById(Guid id, CancellationToken cancellationToken);
        public Task<bool> CreateAsync(T entity, CancellationToken cancellationToken);
        public Task<bool> UpdateAsync(T entity, CancellationToken cancellationToken);
        public Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken);

    }
}
