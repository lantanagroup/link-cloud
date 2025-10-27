using DataAcquisition.Domain.Application.Models;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Models
{

    [DataContract]
    public class ApiFhirQueryConfigurationModel
    {
        [DataMember]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public string? Id { get; set; }

        [DataMember]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public string? FacilityId { get; set; }

        [DataMember]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? FhirServerBaseUrl { get; set; }

        [DataMember]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public AuthenticationConfigurationModel? Authentication { get; set; }

        [DataMember]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? MaxConcurrentRequests { get; set; } = 8;

        [DataMember]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonConverter(typeof(TimeSpanConverter))]
        public TimeSpan? MinAcquisitionPullTime { get; set; }

        [DataMember]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonConverter(typeof(TimeSpanConverter))]
        public TimeSpan? MaxAcquisitionPullTime { get; set; }

        [DataMember]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public string? TimeZone { get; set; }   
    }
}
