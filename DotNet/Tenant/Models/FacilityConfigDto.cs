using System.Runtime.Serialization;

namespace LantanaGroup.Link.Tenant.Models
{
    [DataContract]
    public class FacilityConfigDto
    {
        [DataMember]
        public string? Id { get; set; }
        public string? FacilityId { get; set; }
        [DataMember]
        public string? FacilityName { get; set; }
        public string TimeZone { get; set; }
        public ScheduledReportDto ScheduledReports { get; set; } = null!;

    }
}
