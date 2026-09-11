namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.FacilityAdministration;

public class FhirServerInfoResponse
{
    public string? FhirServerBaseUrl { get; set; }
    public int? MaxConcurrentRequests { get; set; }
    public int? MaxRetries { get; set; }

    /// <summary>HH:MM, facility-local.</summary>
    public string? MinAcquisitionPullTime { get; set; }
    public string? MaxAcquisitionPullTime { get; set; }
    public int? LagDays { get; set; }
    public int? LagHours { get; set; }
    public int? LagMinutes { get; set; }
}
