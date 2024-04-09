namespace LantanaGroup.Link.DemoApiGateway.Application.models.interfaces
{
    public interface IPaginationMetadata
    {
        int PageSize { get; }
        int PageNumber { get; }
        long TotalCount { get; }
    }
}
