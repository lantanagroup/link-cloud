namespace LantanaGroup.Link.Tenant.Models
{
    public class FacilityConfigDto
    {
        public string? Id { get; set; }
        public string FacilityId { get; set; } = null!;
        public string? FacilityName { get; set; }
        public ScheduledReportDto ScheduledReports { get; set; } = null!;

    }
}
