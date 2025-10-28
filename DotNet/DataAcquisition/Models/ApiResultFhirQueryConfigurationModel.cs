using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Serializers;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Models
{
    public class ApiResultFhirQueryConfigurationModel
    {
        public required string Id { get; set; }

        public required string FacilityId { get; set; }

        public required string FhirServerBaseUrl { get; set; }

        public AuthenticationConfigurationModel? Authentication { get; set; }

        public int? MaxConcurrentRequests { get; set; } = 8;

        [JsonConverter(typeof(TimeSpanConverter))]
        public TimeSpan? MinAcquisitionPullTime { get; set; }

        [JsonConverter(typeof(TimeSpanConverter))]
        public TimeSpan? MaxAcquisitionPullTime { get; set; } 
    }
}
