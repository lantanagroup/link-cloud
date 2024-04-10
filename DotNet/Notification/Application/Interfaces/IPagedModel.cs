using LantanaGroup.Link.Notification.Application.Models;

namespace LantanaGroup.Link.Notification.Application.Interfaces
{
    public interface IPagedModel<T> where T : class
    {
        public List<T> Records { get; set; }
        public PaginationMetadata Metadata { get; set; }
    }
}
