namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.FacilityAdministration;

/// <summary>
/// Wire shape for Data Acquisition's clean-replace <c>PUT /api/data/fhirQueryConfiguration</c> —
/// declared locally because LinkSdk's IDataAcquisitionServiceClient doesn't wrap this endpoint yet.
/// Callers must send the complete record; Data Acquisition does not merge.
/// </summary>
public sealed class UpdateFhirQueryConfigurationPayload
{
    public string Id { get; init; } = string.Empty;
    public string FacilityId { get; init; } = string.Empty;
    public string FhirServerBaseUrl { get; init; } = string.Empty;
    public int MaxConcurrentRequests { get; init; }
    public int? MaxRetries { get; init; }
    public TimeSpan? MinAcquisitionPullTime { get; init; }
    public TimeSpan? MaxAcquisitionPullTime { get; init; }
    public string? TimeZone { get; init; }
}
