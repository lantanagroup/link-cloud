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

    private static IReadOnlyList<TimezoneResponse> BuildTimezones() =>
        TimeZoneInfo.GetSystemTimeZones()
            .Select(zone => new TimezoneResponse
            {
                Id = zone.Id,
                DisplayName = $"{zone.Id} — {zone.DisplayName}",
                BaseUtcOffset = zone.BaseUtcOffset
            })
            .OrderBy(zone => zone.BaseUtcOffset)
            .ThenBy(zone => zone.Id, StringComparer.Ordinal)
            .ToArray();
}
