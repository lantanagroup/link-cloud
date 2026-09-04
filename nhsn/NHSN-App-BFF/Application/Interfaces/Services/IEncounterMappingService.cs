using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Encounter;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

// The Encounter Mapping step's local-code-to-CPT/SNOMED rows, for the authenticated caller's
// facility. Backed by Normalization's Code Map operation, not a BFF-owned table — see
// EncounterMappingService for the INormalizationServiceClient wiring.
public interface IEncounterMappingService
{
    Task<IReadOnlyList<EncounterMapping>> GetAsync(CancellationToken cancellationToken = default);

    // Replaces every row for the facility with this set — not an append, unlike Acknowledgement.
    Task SaveAsync(IReadOnlyList<EncounterMapping> mappings, CancellationToken cancellationToken = default);
}
