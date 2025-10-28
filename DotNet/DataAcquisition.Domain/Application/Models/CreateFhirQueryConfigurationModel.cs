using DataAcquisition.Domain.Application.Models;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

public class CreateFhirQueryConfigurationModel
{
    public string? FacilityId { get; set; }
    public string? FhirServerBaseUrl { get; set; }
    public AuthenticationConfigurationModel? Authentication { get; set; }
    public int? MaxConcurrentRequests { get; set; } = 8;
    public TimeSpan? MinAcquisitionPullTime { get; set; }
    public TimeSpan? MaxAcquisitionPullTime { get; set; }
    public string? TimeZone { get; set; } = null;
    public DateTime? CreateDate { get; set; }
    public DateTime? ModifyDate { get; set; }
}