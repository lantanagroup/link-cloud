using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.FacilityAdministration;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;

/// <summary>
/// Data Acquisition operations LinkSdk's IDataAcquisitionServiceClient doesn't wrap yet: the
/// connectionValidation reachability probe, and a clean-replace update for the FHIR query
/// configuration. Extending the SDK for these is scheduled for Onboarding Phase 2 (per
/// 02-bff-architecture.md); until then this calls Data Acquisition directly from the BFF, the same
/// way DotNet/Shared's TenantApiService already calls Tenant directly rather than through a
/// generated client.
/// </summary>
public interface IDataAcquisitionRawClient
{
    /// <summary>
    /// URL-only reachability probe (C8) — no facility configuration need exist yet, and it carries
    /// no authentication block, so a success here proves the server is reachable, not that Link's
    /// own credentials can pull data from it.
    /// </summary>
    Task<FhirConnectionProbeResult> ValidateFhirServerConnectionAsync(string fhirServerBaseUrl, CancellationToken cancellationToken = default);

    /// <summary>Clean-replace update — callers must send the complete record, not a sparse patch.</summary>
    Task UpdateFhirQueryConfigurationAsync(UpdateFhirQueryConfigurationPayload payload, CancellationToken cancellationToken = default);
}
