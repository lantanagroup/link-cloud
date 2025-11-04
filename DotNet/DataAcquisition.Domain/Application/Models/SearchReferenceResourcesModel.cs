using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Enums;

namespace DataAcquisition.Domain.Application.Models
{
    public class SearchReferenceResourcesModel
    {
        public string? FacilityId { get; set; }
        public string? ResourceId { get; set; }
        public string? ResourceType { get; set; }
        public QueryPhase? QueryPhase { get; set; }
        public long? DataAcquisitionLogId { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SortBy { get; set; } = "Id";
        public SortOrder SortOrder { get; set; } = SortOrder.Ascending;
        public List<string> ResourceIds { get; set; }
    }
}
