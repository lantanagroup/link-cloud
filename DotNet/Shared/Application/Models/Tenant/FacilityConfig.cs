using System.Runtime.Serialization;

namespace LantanaGroup.Link.Shared.Application.Models.Tenant
{
    [DataContract]
    public class FacilityConfig
    {
        [DataMember]
        public string? Id { get; set; }
        public string? FacilityId { get; set; }
        [DataMember]
        public string? FacilityName { get; set; }
        public string TimeZone { get; set; }
        public TenantScheduledReportConfig ScheduledReports { get; set; } = null!;

    }
}
