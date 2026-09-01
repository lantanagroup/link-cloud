using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reference;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.VendorProfiles;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Reference;

// BFF-owned reference data: the vendor profiles and the time zone list.
public sealed class ReferenceDataService : IReferenceDataService
{
    // Curated US time zone ids, in display order.
    private static readonly string[] OrderedTimezoneIds =
    {
        "America/New_York",
        "America/Detroit",
        "America/Kentucky/Louisville",
        "America/Kentucky/Monticello",
        "America/Indiana/Indianapolis",
        "America/Indiana/Vincennes",
        "America/Indiana/Winamac",
        "America/Indiana/Marengo",
        "America/Indiana/Petersburg",
        "America/Indiana/Vevay",
        "America/Indiana/Tell_City",
        "America/Indiana/Knox",
        "America/Chicago",
        "America/Menominee",
        "America/North_Dakota/Center",
        "America/North_Dakota/New_Salem",
        "America/North_Dakota/Beulah",
        "America/Denver",
        "America/Boise",
        "America/Phoenix",
        "America/Los_Angeles",
        "America/Anchorage",
        "America/Juneau",
        "America/Sitka",
        "America/Metlakatla",
        "America/Yakutat",
        "America/Nome",
        "America/Adak",
        "Pacific/Honolulu",
        "America/Puerto_Rico",
        "Pacific/Guam",
        "Pacific/Saipan",
        "Pacific/Pago_Pago"
    };

    private static readonly Lazy<IReadOnlyList<TimezoneResponse>> Timezones = new(BuildTimezones);

    public IReadOnlyList<VendorProfile> GetVendorProfiles() => VendorProfileCatalog.All;

    public IReadOnlyList<TimezoneResponse> GetTimezones() => Timezones.Value;

    private static IReadOnlyList<TimezoneResponse> BuildTimezones() =>
        OrderedTimezoneIds
            .Select(id => new TimezoneResponse
            {
                Id = id,
                DisplayName = $"{id} — {TimeZoneInfo.FindSystemTimeZoneById(id).DisplayName}"
            })
            .ToArray();
}
