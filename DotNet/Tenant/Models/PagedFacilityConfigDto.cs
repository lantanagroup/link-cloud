using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Tenant;

namespace LantanaGroup.Link.Tenant.Models
{
    public class PagedFacilityConfigDto
    {
        public List<FacilityConfig> Records { get; set; } = new List<FacilityConfig>();
        public PaginationMetadata Metadata { get; set; } = null!;

        public PagedFacilityConfigDto() { }

        public PagedFacilityConfigDto(List<FacilityConfig> records, PaginationMetadata metadata)
        {
            Records = records;
            Metadata = metadata;
        }
    }
}
