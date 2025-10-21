using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Tenant;

namespace LantanaGroup.Link.Tenant.Models
{
    public class PagedFacilityConfigDto
    {
        public List<FacilityModel> Records { get; set; } = new List<FacilityModel>();
        public PaginationMetadata Metadata { get; set; } = null!;

        public PagedFacilityConfigDto() { }

        public PagedFacilityConfigDto(List<FacilityModel> records, PaginationMetadata metadata)
        {
            Records = records;
            Metadata = metadata;
        }
    }
}
