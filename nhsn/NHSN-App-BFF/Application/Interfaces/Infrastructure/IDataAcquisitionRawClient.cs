using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.FacilityAdministration;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;

/// <summary>
/// Data Acquisition operations that don't fit LinkSdk's IDataAcquisitionServiceClient shape: the
/// connectionValidation reachability probe returns a bespoke result rather than a LinkApiResponse,
/// and the clean-replace update declares its own local payload type. The implementation
/// (DataAcquisitionRawClient) still builds on LinkSdk's LinkApiClientBase for both calls, the same
/// base class the generated clients use, rather than hand-rolling requests against a plain
/// HttpClient.
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
