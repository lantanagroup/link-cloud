using LantanaGroup.Link.Shared.Application.Enums;

namespace LantanaGroup.Link.Census.Models
{
    public class SearchCensusConfigModel
    {
        public string? FacilityId { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SortBy { get; set; } = "Id";
        public SortOrder SortOrder { get; set; } = SortOrder.Ascending;
    }
}
