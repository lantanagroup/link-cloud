using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace LantanaGroup.Link.Shared.Application.Interfaces.Models
{
    public interface IPagedModel<T> where T : class
    {
        public List<T> Records { get; set; }
        public PaginationMetadata Metadata { get; set; }
    }
}
