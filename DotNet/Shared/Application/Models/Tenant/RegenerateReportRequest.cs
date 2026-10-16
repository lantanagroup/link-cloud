using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Shared.Application.Models.Tenant
{
    [DataContract]
    public class RegenerateReportRequest
    {
        [DataMember]
        [JsonPropertyName("reportId")]
        public string? ReportId { get; set; }

        [DataMember]
        [JsonPropertyName("bypassSubmission")]
        public bool? BypassSubmission { get; set; }

        /// <summary>
        /// Optional. "performance" marks an Automation Metrics run. Missing or any other value is lightweight.
        /// </summary>
        [DataMember]
        [JsonPropertyName("metricsMode")]
        public string? MetricsMode { get; set; }
    }
}