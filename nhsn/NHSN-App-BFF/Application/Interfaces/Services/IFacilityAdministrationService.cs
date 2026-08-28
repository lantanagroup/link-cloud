using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.FacilityAdministration;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

public interface IFacilityAdministrationService
{
    /// <summary>
    /// Updates the onboarding flag for <paramref name="facilityId"/>.
    /// </summary>
    /// <remarks>
    /// Takes no acting facility or role argument: both come from <see cref="INhsnUserContext"/>.
    /// The route keeps its facility segment for compatibility, validated against the claim.
    /// </remarks>
    Task<FacilitySummaryResponse?> UpdateFacilityOnboardingAsync(string facilityId, UpdateFacilityOnboardingRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the facility's FHIR server configuration (Data Acquisition's query configuration,
    /// merged with Query Dispatch's discharge-lag schedule). Facility comes from
    /// <see cref="INhsnUserContext"/>, never the route.
    /// </summary>
    Task<FhirServerInfoResponse?> GetFhirServerInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>Writes the facility's FHIR server configuration. FACADMIN only.</summary>
    Task<FhirServerInfoResponse?> UpdateFhirServerInfoAsync(UpdateFhirServerInfoRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// The stage-1, URL-only reachability probe (C8) — proves the FHIR server responds, not that
    /// Link's own credentials can pull data from it. No facility configuration need exist yet.
    /// </summary>
    Task<ConnectionResult> TestFhirConnectionAsync(string fhirServerBaseUrl, CancellationToken cancellationToken = default);
}
