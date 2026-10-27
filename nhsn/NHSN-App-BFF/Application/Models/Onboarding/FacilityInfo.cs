using LantanaGroup.Link.Nhsn.App.Bff.Domain.Enums;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

// The facilityInfo section of FacilityDraft, in our vocabulary.
//
// Owned by Tenant — the BFF persists no copy. Deliberately does not mirror FacilityModel: it
// carries only the four values this step captures. In particular it has no ScheduledReports, an
// arming switch no step here may set.
public sealed record FacilityInfo
{
    public required string FacilityId { get; init; }

    public string? FacilityName { get; init; }

    // IANA time zone id, e.g. America/Chicago.
    public string? TimeZone { get; init; }

    public EhrVendor? Vendor { get; init; }
}
