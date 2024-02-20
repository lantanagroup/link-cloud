namespace LantanaGroup.Link.Notification.Application.Interfaces
{
    public interface IBaseRepository<T> where T : class
    {
        Task<bool> Add(T entity);
        Task<bool> Update(T entity);            
    }
}
