using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reference;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.VendorProfiles;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Reference;

// BFF-owned reference data: the vendor profiles and the time zone list.
public sealed class ReferenceDataService : IReferenceDataService
{
    private static readonly Lazy<IReadOnlyList<TimezoneResponse>> Timezones = new(BuildTimezones);
    private static readonly (string Id, string FriendlyName)[] CuratedZones =
    [
        ("America/New_York", "Eastern Time"),
        ("America/Detroit", "Eastern Time"),
        ("America/Kentucky/Louisville", "Eastern Time"),
        ("America/Kentucky/Monticello", "Eastern Time"),
        ("America/Indiana/Indianapolis", "Eastern Time"),
        ("America/Indiana/Vincennes", "Eastern Time"),
        ("America/Indiana/Winamac", "Eastern Time"),
        ("America/Indiana/Marengo", "Eastern Time"),
        ("America/Indiana/Petersburg", "Eastern Time"),
        ("America/Indiana/Vevay", "Eastern Time"),
        ("America/Indiana/Tell_City", "Central Time"),
        ("America/Indiana/Knox", "Central Time"),
        ("America/Chicago", "Central Time"),
        ("America/Menominee", "Central Time"),
        ("America/North_Dakota/Center", "Central Time"),
        ("America/North_Dakota/New_Salem", "Central Time"),
        ("America/North_Dakota/Beulah", "Central Time"),
        ("America/Denver", "Mountain Time"),
        ("America/Boise", "Mountain Time"),
        ("America/Phoenix", "Mountain Time (no DST)"),
        ("America/Los_Angeles", "Pacific Time"),
        ("America/Anchorage", "Alaska Time"),
        ("America/Juneau", "Alaska Time"),
        ("America/Sitka", "Alaska Time"),
        ("America/Metlakatla", "Alaska Time"),
        ("America/Yakutat", "Alaska Time"),
        ("America/Nome", "Alaska Time"),
        ("America/Adak", "Hawaii-Aleutian Time"),
        ("Pacific/Honolulu", "Hawaii Time (no DST)"),
        ("America/Puerto_Rico", "Atlantic Time (Puerto Rico / US Virgin Islands)"),
        ("Pacific/Guam", "Chamorro Time (Guam)"),
        ("Pacific/Saipan", "Chamorro Time (N. Mariana Islands)"),
        ("Pacific/Pago_Pago", "Samoa Time (American Samoa)")
    ];

    public IReadOnlyList<VendorProfile> GetVendorProfiles() => VendorProfileCatalog.All;

    public IReadOnlyList<TimezoneResponse> GetTimezones() => Timezones.Value;

    private static IReadOnlyList<TimezoneResponse> BuildTimezones() =>
        CuratedZones
            .Select(zone =>
            {
                var info = TimeZoneInfo.FindSystemTimeZoneById(zone.Id);
                return new TimezoneResponse
                {
                    Id = zone.Id,
                    DisplayName = $"{zone.Id} — {zone.FriendlyName}",
                    BaseUtcOffset = info.BaseUtcOffset
                };
            })
            .ToArray();
}
