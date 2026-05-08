namespace LantanaGroup.Link.Shared.Application.Interfaces.Models
{
    public interface IPaginationMetadata
    {
        int PageSize { get; }
        int PageNumber { get; }
        long TotalCount { get; }
    }
}
