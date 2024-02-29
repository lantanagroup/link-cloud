namespace LantanaGroup.Link.Notification.Application.Interfaces
{
    public interface IBaseRepository<T> where T : class
    {
        Task<bool> AddAsync(T entity, CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(T entity, CancellationToken cancellationToken = default);            
    }
}
