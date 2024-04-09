using LantanaGroup.Link.Audit.Application.Models;

namespace LantanaGroup.Link.Audit.Application.Interfaces
{
    public interface IPagedModel<T> where T : class
    {
        public List<T> Records { get; set; }
        public PaginationMetadata Metadata { get; set; }
    }
}
