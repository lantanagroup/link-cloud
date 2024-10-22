using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Tenant.Models;

namespace LantanaGroup.Link.Tenant.Models
{
    public class PagedFacilityConfigDto
    {
        public List<FacilityConfigDto> Records { get; set; } = new List<FacilityConfigDto>();
        public PaginationMetadata Metadata { get; set; } = null!;

        public PagedFacilityConfigDto() { }

        public PagedFacilityConfigDto(List<FacilityConfigDto> records, PaginationMetadata metadata)
        {
            Records = records;
            Metadata = metadata;
        }
    }
}
