using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;

namespace DataAcquisition.Domain.Application.Models
{
    public class SearchQueryPlanModel
    {
        public string? FacilityId { get; set; }
        public string? PlanName { get; set; }
        public Frequency? Type { get; set; }
        public string? EHRDescription { get; set; }
        public string? LookBack { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SortBy { get; set; } = "Id";
        public SortOrder SortOrder { get; set; } = SortOrder.Ascending;
    }
}
