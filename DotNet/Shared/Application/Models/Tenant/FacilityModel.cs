using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Shared.Application.Models.Tenant
{
    [DataContract]
    public class FacilityModel
    {
        [DataMember]
        [JsonPropertyName("id")]
        public Guid? Id { get; set; }

        [JsonPropertyName("facilityId")]
        public string? FacilityId { get; set; }

        [DataMember]
        [JsonPropertyName("facilityName")]
        public string? FacilityName { get; set; }

        [JsonPropertyName("timeZone")]
        public string TimeZone { get; set; } = string.Empty;

        [JsonPropertyName("scheduledReports")]
        public TenantScheduledReportConfig ScheduledReports { get; set; } = new TenantScheduledReportConfig();

    }
}
