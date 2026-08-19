namespace LantanaGroup.Link.DMRP.Models
{
    public class FacilityReportingPlanSearchFilters
    {
        public string? FacilityId { get; set; }

        public string? MeasureMappingId { get; set; }

        public int? Month { get; set; }

        public int? Year { get; set; }

        public bool? IsReporting { get; set; }
    }
}
