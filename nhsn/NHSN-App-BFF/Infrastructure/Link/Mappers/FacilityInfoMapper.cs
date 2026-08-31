using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Enums;
using LantanaGroup.Link.Shared.Application.Models.Tenant;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link.Mappers;

// Translates between Tenant's FacilityModel and our FacilityInfo. Reference implementation for the
// reverse mappers used across the read path.
//
// Pure and side-effect free, which is what makes the read-modify-write invariants below
// unit-testable without a running stack.
internal static class FacilityInfoMapper
{
    public static FacilityInfo ToDomain(FacilityModel source) => new()
    {
        FacilityId = source.FacilityId ?? string.Empty,
        FacilityName = source.FacilityName,
        TimeZone = string.IsNullOrWhiteSpace(source.TimeZone) ? null : source.TimeZone,
        Vendor = ParseVendor(source.Vendor?.Name)
    };

    // Builds the payload for a facility Tenant does not have yet.
    //
    // ScheduledReports is built with three empty arrays — the only place in the BFF permitted to
    // construct it, and the only place empty is safe. Tenant throws on a null-array default, and
    // empty arrays are the correct create-time value: they leave the facility inert, with no Quartz
    // rows created. On an existing facility the same value would silently delete the Quartz job and
    // trigger, which is why Overlay exists instead.
    public static FacilityModel ForCreate(FacilityInfo desired, VendorModel? vendor) => new()
    {
        FacilityId = desired.FacilityId,
        FacilityName = desired.FacilityName,
        TimeZone = desired.TimeZone ?? string.Empty,
        Vendor = vendor,
        ScheduledReports = new TenantScheduledReportConfig
        {
            Daily = [],
            Weekly = [],
            Monthly = []
        }
    };

    // Overlays the values this step owns onto the record Tenant currently holds.
    //
    // current must be the object from the immediately preceding GET — this method mutates it
    // rather than building a new one, so fields the BFF doesn't own (including ScheduledReports)
    // survive untouched even as Tenant's schema evolves. Null on desired means "not captured by
    // this step", not "clear it".
    public static FacilityModel Overlay(FacilityModel current, FacilityInfo desired, VendorModel? vendor)
    {
        ArgumentNullException.ThrowIfNull(current);

        if (desired.FacilityName is not null)
        {
            current.FacilityName = desired.FacilityName;
        }

        if (desired.TimeZone is not null)
        {
            current.TimeZone = desired.TimeZone;
        }

        if (vendor is not null)
        {
            current.Vendor = vendor;
        }

        // ScheduledReports is deliberately untouched. See the remarks above before changing this.
        return current;
    }

    public static EhrVendor? ParseVendor(string? vendorName) =>
        Enum.TryParse<EhrVendor>(vendorName, ignoreCase: true, out var vendor) ? vendor : null;
}
