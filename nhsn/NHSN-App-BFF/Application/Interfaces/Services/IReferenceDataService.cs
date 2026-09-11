using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reference;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.VendorProfiles;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

// Reference data shared across every caller. Vendor profiles/timezones/encounter codes are
// BFF-owned outright — no Link call, no facility scope.
public interface IReferenceDataService
{
    IReadOnlyList<VendorProfile> GetVendorProfiles();

    IReadOnlyList<TimezoneResponse> GetTimezones();

    IReadOnlyList<EncounterCode> GetEncounterCodes(string? query = null);

    Task<IReadOnlyList<HslocCode>> GetHslocCodesAsync(CancellationToken cancellationToken = default);
}
