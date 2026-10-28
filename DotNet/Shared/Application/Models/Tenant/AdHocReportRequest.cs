using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Shared.Application.Models.Tenant
{
    [DataContract]
    public class AdHocReportRequest
    {
        [DataMember]
        [JsonPropertyName("bypassSubmission")]
        public bool? BypassSubmission { get; set; }
        [DataMember]
        [JsonPropertyName("startDate")]
        public DateTime? StartDate { get; set; }
        [DataMember]
        [JsonPropertyName("endDate")]
        public DateTime? EndDate { get; set; }
        [DataMember]
        [JsonPropertyName("reportTypes")]
        public List<string>? ReportTypes { get; set; }
        [DataMember]
        [JsonPropertyName("patientIds")]
        public List<string>? PatientIds { get; set; }

        /// <summary>
        /// Optional. "performance" marks an Automation Metrics run. Missing or any other value is lightweight.
        /// </summary>
        [DataMember]
        [JsonPropertyName("metricsMode")]
        public string? MetricsMode { get; set; }
    }
}
