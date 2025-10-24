using LantanaGroup.Link.Shared.Application.Enums;

namespace LantanaGroup.Link.Census.Models
{
    public class SearchCensusPatientListModel
    {
        public string? FacilityId { get; set; }
        public string? PatientId { get; set; }
        public bool ActiveOnly { get; set; } = false;
        public DateTime? AdmitDateStart { get; set; }
        public DateTime? AdmitDateEnd { get; set; }
        public DateTime? DischargeDateStart { get; set; }
        public DateTime? DischargeDateEnd { get; set; }
        public bool DistinctByPatientId { get; set; } = false;
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SortBy { get; set; } = "PatientId";
        public SortOrder SortOrder { get; set; } = SortOrder.Ascending;
    }
}
