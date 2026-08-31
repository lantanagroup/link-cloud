namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.FacilityAdministration;

/// <summary>
/// Shape of the FhirQueryConfiguration JSON returned by Data Acquisition's GetFhirQueryConfigurationAsync
/// SDK call. Used only to deserialize LinkApiResponse.RawBody locally in this BFF, without adding a
/// fully-typed response model to LinkSdk.
/// </summary>
internal class FhirQueryConfigurationDetail
{
    public string? Id { get; set; }
    public string? FacilityId { get; set; }
    public string? FhirServerBaseUrl { get; set; }
    public int? MaxConcurrentRequests { get; set; }
    public int? MaxRetries { get; set; }
    public TimeSpan? MinAcquisitionPullTime { get; set; }
    public TimeSpan? MaxAcquisitionPullTime { get; set; }
}
