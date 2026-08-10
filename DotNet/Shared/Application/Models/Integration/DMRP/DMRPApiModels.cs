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

    public DateTime CreateDate { get; set; }

    public DateTime? ModifyDate { get; set; }
}

public class MeasureMappingModel
{
    public string? Id { get; set; }
}
