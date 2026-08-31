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
