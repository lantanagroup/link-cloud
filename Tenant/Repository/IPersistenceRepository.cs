using LantanaGroup.Link.Tenant.Entities;
using MongoDB.Driver;

namespace LantanaGroup.Link.Tenant.Repository
{
    public interface IPersistenceRepository<T> where T : class
    {
        public Task<List<T>> GetAsync(CancellationToken cancellationToken);
        public Task<T> GetAsyncById(string id, CancellationToken cancellationToken);
        public Task<bool> CreateAsync(T entity, CancellationToken cancellationToken);
        public Task<bool> UpdateAsync(string id, T entity, CancellationToken cancellationToken);
        public Task<bool> RemoveAsync(string id, CancellationToken cancellationToken);
        public Task<List<T>> FindAsync(FilterDefinition<T> filter, CancellationToken cancellationToken);
        public List<T> Get();
        public T GetById(string id);
        public bool Create(T entity);
        public bool Update(string id, T entity);
        public bool Remove(string id);
        public Task<bool> HealthCheck();


    }
}
