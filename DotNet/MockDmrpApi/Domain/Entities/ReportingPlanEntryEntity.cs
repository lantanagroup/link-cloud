using LantanaGroup.Link.Shared.Domain.Entities;

namespace LantanaGroup.Link.MockDmrpApi.Domain.Entities;

/// <summary>
/// The NHSN component a reporting plan entry belongs to. The two components differ in
/// subject and in cadence, which is why the reporting period differs with them.
/// </summary>
public static class ReportingComponents
{
    /// <summary>Medicine reports. Reported monthly, so entries carry a month.</summary>
    public const string Msc = "MSC";

    /// <summary>Patient safety. Reported annually, so entries carry no month.</summary>
    public const string Ps = "PS";

    public static readonly string[] All = [Msc, Ps];

    public static bool IsKnown(string? component) =>
        All.Contains(component, StringComparer.OrdinalIgnoreCase);

    /// <summary>True for components reported monthly, which are the ones that need a month.</summary>
    public static bool RequiresReportingMonth(string? component) =>
        string.Equals(component, Msc, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the canonical casing of a known component, or the input unchanged if it is
    /// not one. Callers validate with <see cref="IsKnown"/> first.
    /// </summary>
    public static string Normalize(string? component) =>
        All.FirstOrDefault(c => string.Equals(c, component, StringComparison.OrdinalIgnoreCase))
        ?? component
        ?? string.Empty;
}

/// <summary>
/// One row of a reporting plan: facility <c>F</c> is reporting measure <c>M</c> for a given
/// period, within a given NHSN component.
/// </summary>
/// <remarks>
/// Absence of a row for a (facility, component, measure, period) combination means the
/// facility is NOT enrolled in that measure. There is no negative representation, which is
/// why <see cref="IsReporting"/> is currently always <c>"Y"</c> wherever a row exists.
/// <para>
/// <see cref="ReportingMonth"/> is nullable because the two components are reported on
/// different cadences: MSC is monthly and carries a month, PS is annual and does not. That
/// rule is conditional rather than structural, so the service enforces it rather than the
/// column.
/// </para>
/// <para>
/// Kept separate from the generated contract types on purpose. Persisting a generated type
/// would tie the database schema to the API contract and turn every revision of
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

    /// <summary>The NHSN component. See <see cref="ReportingComponents"/>.</summary>
    public string Component { get; set; } = string.Empty;

    /// <summary>NHSN measure short name, for example <c>HOB</c> or <c>HTCDI</c>.</summary>
    public string Measure { get; set; } = string.Empty;

    /// <summary>
    /// The reporting month, for monthly components. Null for annual components, where a
    /// month has no meaning.
    /// </summary>
    public int? ReportingMonth { get; set; }

    public int ReportingYear { get; set; }

    /// <summary>
    /// Currently always <c>"Y"</c>. Kept as a string rather than a bool to mirror the
    /// upstream field, which is not known to be boolean.
    /// </summary>
    public string IsReporting { get; set; } = "Y";
}
