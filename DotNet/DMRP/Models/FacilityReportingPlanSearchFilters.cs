using System.Runtime.Serialization;

namespace LantanaGroup.Link.DMRP.Models
{
    [DataContract]
    public class FacilityReportingPlanSearchFilters
    {
        [DataMember]
        public string? FacilityId { get; set; }

        [DataMember]
        public string? MeasureMappingId { get; set; }

        [DataMember]
        public int? Month { get; set; }

        [DataMember]
        public int? Year { get; set; }

        [DataMember]
        public bool? IsReporting { get; set; }
    }
}
