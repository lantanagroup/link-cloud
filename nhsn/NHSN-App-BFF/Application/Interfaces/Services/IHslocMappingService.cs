using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Hsloc;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

// The HSLOC Location Identification step's local-code-to-HSLOC rows, for the authenticated
// caller's facility. Backed by Normalization's Code Map operation, not a BFF-owned table — mirrors
// IEncounterMappingService / EncounterMappingService's INormalizationServiceClient wiring, on the
// Location resource instead of Encounter. See HslocMappingService for the FhirPath/system caveats.
public interface IHslocMappingService
{
    Task<IReadOnlyList<HslocMapping>> GetAsync(CancellationToken cancellationToken = default);

    // Replaces every row for the facility with this set — not an append, unlike Acknowledgement.
    Task SaveAsync(IReadOnlyList<HslocMapping> mappings, CancellationToken cancellationToken = default);
}
