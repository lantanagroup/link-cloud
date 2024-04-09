namespace LantanaGroup.Link.DemoApiGateway.Application.models.interfaces
{
    public interface IPagedModel<T> where T : class
    {
        public List<T> Records { get; set; }
        public PaginationMetadata Metadata { get; set; }
    }
}
