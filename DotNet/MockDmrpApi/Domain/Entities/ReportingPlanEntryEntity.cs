using LantanaGroup.Link.Shared.Domain.Entities;

namespace LantanaGroup.Link.MockDmrpApi.Domain.Entities;

/// <summary>
/// One row of a reporting plan: facility <c>F</c> is reporting measure <c>M</c>
/// for a given month and year.
/// </summary>
/// <remarks>
/// Absence of a row for a (facility, measure, period) combination means the
/// facility is NOT enrolled in that measure for that period. There is no
/// negative representation, which is why <see cref="IsReporting"/> is currently
/// always <c>"Y"</c> wherever a row exists.
/// <para>
/// This is deliberately kept separate from the generated
/// <c>ReportingPlanEntry</c> contract type. Persisting the generated type would
/// tie the database schema to the API contract and turn every revision of
/// Contracts/dmrp-openapi.yaml into a migration.
/// </para>
/// <para>
/// <c>Id</c>, <c>CreateDate</c> and <c>ModifyDate</c> come from
/// <see cref="BaseEntityExtended"/>; the timestamps are maintained by
/// <c>UpdateBaseEntityInterceptor</c>, not by this class.
/// </para>
/// </remarks>
public class ReportingPlanEntryEntity : BaseEntityExtended
{
    /// <summary>Identifier of the reporting facility, as known to the Tenant service.</summary>
    public string FacilityId { get; set; } = string.Empty;

    /// <summary>NHSN measure (module) short name, for example <c>HOB</c> or <c>HTCDI</c>.</summary>
    public string Measure { get; set; } = string.Empty;

    public int ReportingMonth { get; set; }

    public int ReportingYear { get; set; }

    /// <summary>
    /// Currently always <c>"Y"</c>. Kept as a string rather than a bool to mirror
    /// the upstream field, which is not known to be boolean.
    /// </summary>
    public string IsReporting { get; set; } = "Y";
}
