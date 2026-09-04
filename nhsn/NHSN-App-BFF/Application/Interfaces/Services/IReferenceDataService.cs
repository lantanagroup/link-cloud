using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reference;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.VendorProfiles;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

// Reference data the BFF owns outright — no Link call, no facility scope. Deliberately
// facility-independent: these answers are the same for every caller, which is what lets the UI
// branch on data rather than hardcoding vendor names.
public interface IReferenceDataService
{
    IReadOnlyList<VendorProfile> GetVendorProfiles();

    IReadOnlyList<TimezoneResponse> GetTimezones();

    IReadOnlyList<EncounterCode> GetEncounterCodes(string? query = null);
}
