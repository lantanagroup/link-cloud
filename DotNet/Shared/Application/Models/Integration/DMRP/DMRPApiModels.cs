using LantanaGroup.Link.Shared.Application.Models.Tenant;
using System.ComponentModel.DataAnnotations;
namespace LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;

public class FacilityReportingPlanModel
{
    public string? Id { get; set; }

    /// <summary>
    /// The reporting facility as known to the Tenant service (the NHSN Org Id).
    /// </summary>
    public string? FacilityId { get; set; }

    /// <summary>
    /// The measure mapping this plan reports against.
    /// </summary>
    public string? MeasureMappingId { get; set; }

    /// <summary>
    /// The NHSN component the enrollment belongs to - MSC or PS. Both are reported monthly.
    /// </summary>
    public string? Component { get; set; }

    /// <summary>
    /// Month of the reporting period, 1-12.
    /// </summary>
    public int ReportingMonth { get; set; }

    public int ReportingYear { get; set; }

    /// <summary>
    /// Whether the facility was reporting the measure during the period.
    /// </summary>
    public bool IsReporting { get; set; }

    /// <summary>
    /// The NHSN measure of the mapping this plan reports against. Populated by reads that resolve
    /// the mapping (the per-facility view); null elsewhere.
    /// </summary>
    public string? Measure { get; set; }

    /// <summary>
    /// The digital quality measure the mapping evaluates. Populated alongside <see cref="Measure"/>.
    /// </summary>
    public string? DQM { get; set; }

    /// <summary>
    /// The reporting cadence the mapping carries. Populated alongside <see cref="Measure"/>.
    /// </summary>
    public Frequency? Frequency { get; set; }

    public DateTime CreateDate { get; set; }

    public DateTime? ModifyDate { get; set; }
}

/// <summary>
/// A facility's reporting obligations for one period: every measure it is enrolled to report in a
/// single month and year.
/// </summary>
/// <remarks>
/// The stored grain is one row per measure per period, which is what the reporting workflow needs.
/// The facility-facing look-ahead is a table of periods, so the grouping is done here rather than
/// left to each client - two clients grouping the same rows two ways is how the same plan starts
/// rendering differently in two places.
/// </remarks>
public class FacilityReportingPlanPeriodModel
{
    public int ReportingYear { get; set; }

    /// <summary>
    /// The NHSN component the enrollment belongs to - MSC or PS. Both are reported monthly.
    /// </summary>
    public string? Component { get; set; }

    /// <summary>
    /// Month of the reporting period, 1-12.
    /// </summary>
    public int ReportingMonth { get; set; }

    /// <summary>
    /// The measures enrolled in this period.
    /// </summary>
    public List<FacilityReportingPlanMeasureModel> Measures { get; set; } = [];

    /// <summary>
    /// What Link will actually run for this period: the dQMs grouped by the cadence they report on.
    /// Derived from <see cref="Measures"/> by the same rule that builds the facility's stored
    /// schedule, so the look-ahead cannot promise a report the scheduler will not create.
    /// </summary>
    /// <remarks>
    /// Narrower than <see cref="Measures"/> on purpose. A measure with no dQM mapped, or one whose
    /// cadence Link has no timer for (Discharge, Adhoc), is listed as an enrollment but produces no
    /// scheduled report - which is the difference the facility needs to see.
    /// </remarks>
    public TenantScheduledReportConfig Schedule { get; set; } = new()
    {
        Daily = [],
        Weekly = [],
        Monthly = []
    };

    /// <summary>
    /// True when this period has no reporting plan on record and was derived from the facility's
    /// current enrollment instead.
    /// </summary>
    /// <remarks>
    /// A projection assumes the current enrollment continues. DMRP records enrollment per period, so
    /// once a period's own rows exist they are reported as they are and this is false - recorded
    /// always wins over projected.
    /// </remarks>
    public bool IsProjected { get; set; }
}

/// <summary>
/// One measure inside a <see cref="FacilityReportingPlanPeriodModel"/>, already resolved through its
/// measure mapping so the period table renders without a second call to the measure-mappings API.
/// </summary>
public class FacilityReportingPlanMeasureModel
{
    /// <summary>
    /// The measure mapping this plan reports against.
    /// </summary>
    public string? MeasureMappingId { get; set; }

    /// <summary>
    /// The NHSN measure the facility enrolled in, such as HOB.
    /// </summary>
    public string? Measure { get; set; }

    /// <summary>
    /// The digital quality measure the mapping evaluates, or null when the mapping could not be
    /// resolved.
    /// </summary>
    public string? DQM { get; set; }

    /// <summary>
    /// The reporting cadence the mapping carries.
    /// </summary>
    public Frequency? Frequency { get; set; }

    /// <summary>
    /// Whether the facility is reporting the measure for the period. A withdrawn enrollment is
    /// recorded as false rather than removed, so it can be shown as history instead of vanishing.
    /// </summary>
    public bool IsReporting { get; set; }
}

public class MeasureMappingModel
{
    public string? Id { get; set; }

    [Required]
    [StringLength(255)]
    public string? Measure { get; set; }

    [Required]
    [StringLength(255)]
    public string? DQM { get; set; }

    [Required]
    [EnumDataType(typeof(Frequency))]
    public Frequency? Frequency { get; set; }
}
