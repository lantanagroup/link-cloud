namespace LantanaGroup.Link.Census.Application.Interfaces
{
    public interface IBaseRepository<T> where T : class
    {
        bool Add(T entity);
        Task<bool> AddAsync(T entity);
        T Get(Guid id);
        Task<T> GetAsync(Guid id);
        IEnumerable<T> GetAll();
        Task<IEnumerable<T>> GetAllAsync();
        bool Exists(Guid id);
        Task<bool> ExistsAsync(Guid id);
        bool Update(Guid id, T entity);
        Task<bool> UpdateAsync(Guid id, T entity);
        bool Delete(Guid id);
        Task<bool> DeleteAsync(Guid id);

    }
}
