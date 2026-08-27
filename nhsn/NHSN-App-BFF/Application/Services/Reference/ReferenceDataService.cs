using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reference;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.VendorProfiles;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Reference;

// BFF-owned reference data: the vendor profiles and the time zone list.
public sealed class ReferenceDataService : IReferenceDataService
{
    private static readonly Lazy<IReadOnlyList<TimezoneResponse>> Timezones = new(BuildTimezones);

    public IReadOnlyList<VendorProfile> GetVendorProfiles() => VendorProfileCatalog.All;

    public IReadOnlyList<TimezoneResponse> GetTimezones() => Timezones.Value;

    // Builds the time zone list as IANA ids, sorted by offset, computed once.
    //
    // Explicitly converted rather than returned raw: TimeZoneInfo.GetSystemTimeZones() returns
    // Windows ids on Windows and IANA ids on Linux, and Tenant stores IANA (America/Chicago), so a
    // Windows id written through would only fail later, in a service that can't resolve it.
    //
    // The full list is returned rather than a curated US subset — which zones a facility may choose
    // is presentation policy, and filtering here would silently exclude a territory nobody thought
    // of at build time.
    private static IReadOnlyList<TimezoneResponse> BuildTimezones() =>
        TimeZoneInfo.GetSystemTimeZones()
            .Select(zone =>
            {
                var id = TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var ianaId) ? ianaId : zone.Id;
                return new TimezoneResponse
                {
                    Id = id,
                    DisplayName = zone.DisplayName,
                    BaseUtcOffset = zone.BaseUtcOffset
                };
            })
            .DistinctBy(zone => zone.Id)
            .OrderBy(zone => zone.BaseUtcOffset)
            .ThenBy(zone => zone.Id, StringComparer.Ordinal)
            .ToArray();
}
