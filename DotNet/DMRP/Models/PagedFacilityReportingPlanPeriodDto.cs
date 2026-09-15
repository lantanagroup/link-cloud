using LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;
using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace LantanaGroup.Link.DMRP.Models
{
    /// <summary>
    /// A page of a facility's reporting plan, grouped by reporting period.
    /// </summary>
    /// <remarks>
    /// Paging counts periods rather than plan rows, so a page of a six-month look-ahead is six
    /// entries however many measures each of them carries. A facility with no plans is an empty
    /// page, not a 404.
    /// </remarks>
    public class PagedFacilityReportingPlanPeriodDto
    {
        public List<FacilityReportingPlanPeriodModel> Records { get; set; } = new List<FacilityReportingPlanPeriodModel>();

        public PaginationMetadata Metadata { get; set; } = null!;

        public PagedFacilityReportingPlanPeriodDto() { }

        public PagedFacilityReportingPlanPeriodDto(List<FacilityReportingPlanPeriodModel> records, PaginationMetadata metadata)
        {
            Records = records;
            Metadata = metadata;
        }
    }
}
