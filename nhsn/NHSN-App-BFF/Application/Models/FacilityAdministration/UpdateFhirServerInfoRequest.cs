namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.FacilityAdministration;

public class UpdateFhirServerInfoRequest
{
    public string FhirServerBaseUrl { get; set; } = string.Empty;
    public int MaxConcurrentRequests { get; set; }
    public int MaxRetries { get; set; }
    public string MinAcquisitionPullTime { get; set; } = string.Empty;
    public string MaxAcquisitionPullTime { get; set; } = string.Empty;
    public int LagDays { get; set; }
    public int LagHours { get; set; }
    public int LagMinutes { get; set; }
}
