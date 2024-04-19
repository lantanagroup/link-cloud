

 namespace LantanaGroup.Link.Tenant.Models
{
    public class PaginationMetadata
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
