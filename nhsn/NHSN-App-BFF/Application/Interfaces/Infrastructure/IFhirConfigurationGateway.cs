using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;

// Data Acquisition's FHIR query configuration, in our vocabulary.
public interface IFhirConfigurationGateway
{
    // Reads the facility's FHIR configuration, or null when none exists yet.
    Task<FhirSection?> GetAsync(string facilityId, CancellationToken cancellationToken = default);

    Task SaveAsync(FhirConfigurationSave request, CancellationToken cancellationToken = default);

    // URL-only FHIR reachability probe, before any configuration has been saved:
    // IDataAcquisitionServiceClient.ValidateFhirServerConnectionAsync, which has Data Acquisition
    // request the server's /metadata CapabilityStatement. A rejected URL or an unreachable server
    // is a failed probe, not an error; Detail carries Data Acquisition's explanation.
    Task<FhirConnectionProbeResult> TestConnectionAsync(string fhirServerBaseUrl, CancellationToken cancellationToken = default);
}

public sealed record FhirConnectionProbeResult(bool IsConnected, string? Detail = null);

public sealed record FhirConfigurationSave
{
    public required string FacilityId { get; init; }
    public required string FhirServerBaseUrl { get; init; }
    public required int MaxConcurrentRequests { get; init; }
    public required int MaxRetries { get; init; }
    // Optional as a pair - both null means Data Acquisition applies no pull-time window.
    public TimeSpan? MinAcquisitionPullTime { get; init; }
    public TimeSpan? MaxAcquisitionPullTime { get; init; }
    public string? TimeZone { get; init; }
}
