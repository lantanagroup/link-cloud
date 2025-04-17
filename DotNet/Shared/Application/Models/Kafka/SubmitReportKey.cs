namespace LantanaGroup.Link.Shared.Application.Models
{
    public class SubmitReportKey
    {
        public string FacilityId { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}
