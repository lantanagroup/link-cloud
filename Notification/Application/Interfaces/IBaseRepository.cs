namespace LantanaGroup.Link.Notification.Application.Interfaces
{
    public interface IBaseRepository<T> where T : class
    {
        bool Add(T entity);
        Task<bool> AddAsync(T entity);
        T Get(string id);
        Task<T> GetAsync(string id);
        IEnumerable<T> GetAll();
        Task<IEnumerable<T>> GetAllAsync();
        bool Exists(string id);
        Task<bool> ExistsAsync(string id);
        Task<bool> HealthCheck();
    }
}
