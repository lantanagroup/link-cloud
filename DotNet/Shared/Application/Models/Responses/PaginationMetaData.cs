using LantanaGroup.Link.Shared.Application.Interfaces.Models;

namespace LantanaGroup.Link.Shared.Application.Models.Responses
{
    public class PaginationMetadata : IPaginationMetadata
    {
        public int PageSize { get; set; }
        public int PageNumber { get; set; }
        public long TotalCount { get; set; }
        public long TotalPages { get; set; }

        public PaginationMetadata() { }

        public PaginationMetadata(int pageSize, int pageNumber, long totalCount)
        {
            PageSize = pageSize;
            PageNumber = pageNumber;
            TotalCount = totalCount;
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        }
    }
}
