using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;

// Data Acquisition's FHIR query configuration, in our vocabulary.
public interface IFhirConfigurationGateway
{
    // Reads the facility's FHIR configuration, or null when none exists yet.
    Task<FhirSection?> GetAsync(string facilityId, CancellationToken cancellationToken = default);

    Task SaveAsync(FhirConfigurationSave request, CancellationToken cancellationToken = default);

    // URL-only FHIR reachability probe, before any configuration has been saved:
    // IDataAcquisitionServiceClient.ValidateConnectionAsync. DataAcquisition does not expose an
    // unscoped validate route yet, so that SDK method answers with a synthetic success today and
    // makes no network call — see its own doc comment. A true result here means "not rejected by
    // Link", not "reachable", until that backend route exists; FacilityAdministrationService
    // reports this honestly to the caller via ConnectionResult.Simulated.
    Task<bool> TestConnectionAsync(string fhirServerBaseUrl, CancellationToken cancellationToken = default);
}

public sealed record FhirConfigurationSave
{
    public required string FacilityId { get; init; }
    public required string FhirServerBaseUrl { get; init; }
    public required int MaxConcurrentRequests { get; init; }
    public required int MaxRetries { get; init; }
    public required TimeSpan MinAcquisitionPullTime { get; init; }
    public required TimeSpan MaxAcquisitionPullTime { get; init; }
    public string? TimeZone { get; init; }
}
