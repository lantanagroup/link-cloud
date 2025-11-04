using DataAcquisition.Domain.Application.Models;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog
{
    public class UpdateFhirQueryConfigurationModel
    {
        public string? Id { get; set; }
        public string? FacilityId { get; set; }
        public string? FhirServerBaseUrl { get; set; }
        public AuthenticationConfigurationModel? Authentication { get; set; }
        public int? MaxConcurrentRequests { get; set; } = 8;
        public TimeSpan? MinAcquisitionPullTime { get; set; }
        public TimeSpan? MaxAcquisitionPullTime { get; set; }
    }
}
