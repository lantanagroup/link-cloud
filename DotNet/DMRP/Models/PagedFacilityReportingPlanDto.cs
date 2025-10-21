using LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;
using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace LantanaGroup.Link.DMRP.Models
{
    public class PagedFacilityReportingPlanDto
    {
        public List<FacilityReportingPlanModel> Records { get; set; } = new List<FacilityReportingPlanModel>();
        public PaginationMetadata Metadata { get; set; } = null!;

        public PagedFacilityReportingPlanDto() { }

        public PagedFacilityReportingPlanDto(List<FacilityReportingPlanModel> records, PaginationMetadata metadata)
        {
            Records = records;
            Metadata = metadata;
        }
    }
}
