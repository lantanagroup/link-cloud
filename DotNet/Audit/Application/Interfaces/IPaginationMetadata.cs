namespace LantanaGroup.Link.Audit.Application.Interfaces
{
    public interface IPaginationMetadata
    {      
        int PageSize { get; }
        int PageNumber { get; }
        long TotalCount { get; }
    }
}
