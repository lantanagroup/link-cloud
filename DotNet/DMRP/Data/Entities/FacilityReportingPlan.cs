using LantanaGroup.Link.Shared.Domain.Entities;

namespace LantanaGroup.Link.DMRP.Data.Entities;

/// <summary>
/// What the DMRP API said a facility is enrolled to report for a single measure in a single
/// reporting period. One row per facility / measure mapping / month / year.
/// </summary>
public class FacilityReportingPlan : BaseEntityExtended
{
    /// <summary>
    /// The reporting facility as it is known to the Tenant service (the NHSN Org Id). Not a database
    /// relationship: facilities live in the host service's own tables, so integrity is enforced by
    /// the manager rather than by a foreign key.
    /// </summary>
    public string FacilityId { get; set; } = string.Empty;

    /// <summary>
    /// The NHSN measure the facility is enrolled in, as DMRP names it - HOB, HTCDI and so on.
    /// </summary>
    /// <remarks>
    /// Stored on the row rather than read through the mapping, because a measure Link has no
    /// mapping for still has to be recorded. Without it such a row could not say what the facility
    /// is enrolled in, which is the one thing an admin needs in order to go and map it.
    /// </remarks>
    public string Measure { get; set; } = string.Empty;

    /// <summary>
    /// The mapping this plan reports against, relating the NHSN measure to the digital quality
    /// measure (dQM) Link evaluates patients against. A single-column foreign key to
    /// MeasureMappings.Id.
    /// </summary>
    /// <remarks>
    /// Null when Link has no mapping for the measure yet. DMRP decides what a facility reports and
    /// an admin decides how Link evaluates it, so the enrollment is recorded either way and the
    /// mapping is filled in afterwards through the measure-mappings screen. An unmapped enrollment
    /// schedules nothing until then - visible rather than lost.
    /// </remarks>
    public string? MeasureMappingId { get; set; }

    public MeasureMapping? MeasureMapping { get; set; }

    /// <summary>
    /// The NHSN component the enrollment belongs to - MSC or PS. Both are reported monthly; the
    /// component says which of DMRP's two operations the enrollment came from, not how often it is
    /// reported.
    /// </summary>
    /// <remarks>
    /// Recorded rather than inferred from the measure. DMRP returns the two components from separate
    /// operations, so which one an enrollment came from is a fact about the read, and a measure that
    /// later moves component would otherwise silently reinterpret every row already stored for it.
    /// </remarks>
    public string Component { get; set; } = ReportingComponents.Msc;

    /// <summary>
    /// Month of the reporting period, 1-12.
    /// </summary>
    public int ReportingMonth { get; set; }

    public int ReportingYear { get; set; }

    /// <summary>
    /// Whether the facility was reporting this measure for the period. A measure the facility has
    /// stopped reporting is set to false rather than removed, so the row keeps the history of what
    /// DMRP said and when it changed.
    /// </summary>
    public bool IsReporting { get; set; }
}
