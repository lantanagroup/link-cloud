
namespace LantanaGroup.Link.Shared.Application.Models.Responses
{
    public class PagedConfigModel <T> where T : class
    {
        public List<T> Records { get; set; } = new List<T>();
        public PaginationMetadata Metadata { get; set; } = null!;

        public PagedConfigModel() { }

        public PagedConfigModel(List<T> records, PaginationMetadata metadata)
        {
            Records = records;
            Metadata = metadata;
        }

     }
}
