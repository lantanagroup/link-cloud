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
    /// The mapping this plan reports against, relating the NHSN measure to the digital quality
    /// measure (dQM) Link evaluates patients against. A single-column foreign key to
    /// MeasureMappings.Id; the composite over (FacilityId, MeasureMappingId, ReportingMonth,
    /// ReportingYear) is a unique index, not the key.
    /// </summary>
    public string MeasureMappingId { get; set; } = string.Empty;

    public MeasureMapping? MeasureMapping { get; set; }

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
