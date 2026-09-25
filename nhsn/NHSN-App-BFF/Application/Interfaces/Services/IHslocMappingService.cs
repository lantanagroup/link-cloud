using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Hsloc;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

// The HSLOC Location Identification step's local-code-to-HSLOC rows, for the authenticated
// caller's facility. Backed by Normalization's dedicated HSLOC endpoints (hsloc-mappings/*, HSLOC),
public interface IHslocMappingService
{
    Task<IReadOnlyList<HslocMapping>> GetAsync(CancellationToken cancellationToken = default);

    // Replaces every row for the facility with this set — not an append, unlike Acknowledgement.
    Task SaveAsync(IReadOnlyList<HslocMapping> mappings, CancellationToken cancellationToken = default);
}
