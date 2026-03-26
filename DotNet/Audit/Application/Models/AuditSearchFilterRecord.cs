using LantanaGroup.Link.Audit.Infrastructure.Logging;

namespace LantanaGroup.Link.Audit.Application.Models
{
    public record AuditSearchFilterRecord
    {
        [PiiData]
        public string? SearchText { get; init; }
        public string? FilterFacilityBy { get; init; }
        public string? FilterCorrelationBy { get; init; }
        public string? FilterServiceBy { get; init; }
        public string? FilterActionBy { get; init; }            
        public string? FilterUserBy { get; init; }
        public string? SortBy { get; init; }
        public SortOrder SortOrder { get; init; }
        public int PageSize { get; init; }
        public int PageNumber { get; init; }

        public AuditSearchFilterRecord(string? searchText, string? filterFacilityBy, string? filterCorrelationBy, string? filterServiceBy, string? filterActionBy, string? filterUserBy, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber)
        {
            SearchText = searchText;
            FilterFacilityBy = filterFacilityBy;
            FilterCorrelationBy = filterCorrelationBy;
            FilterServiceBy = filterServiceBy;
            FilterActionBy = filterActionBy;
            FilterUserBy = filterUserBy;
            SortBy = sortBy;
            SortOrder = sortOrder ?? SortOrder.Ascending;
            PageSize = pageSize;
            PageNumber = pageNumber;
        }

    }
}
