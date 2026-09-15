using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reference;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.VendorProfiles;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

// Reference data shared across every caller. Vendor profiles/timezones are BFF-owned outright —
// no Link call, no facility scope. Encounter codes and HSLOC codes are read live from Terminology
// and Normalization respectively.
public interface IReferenceDataService
{
    IReadOnlyList<VendorProfile> GetVendorProfiles();

    IReadOnlyList<TimezoneResponse> GetTimezones();

    Task<IReadOnlyList<EncounterCode>> GetEncounterCodesAsync(CancellationToken cancellationToken = default);

    Task<EncounterCodeDetail?> LookupEncounterCodeAsync(string system, string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HslocCode>> GetHslocCodesAsync(CancellationToken cancellationToken = default);
}
