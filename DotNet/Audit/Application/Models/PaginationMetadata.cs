using LantanaGroup.Link.Audit.Application.Interfaces;

namespace LantanaGroup.Link.Audit.Application.Models
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
