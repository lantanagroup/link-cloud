using System.ComponentModel.DataAnnotations;

namespace LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;

public static class ReportingPeriodLimits
{
    public const int MinimumReportingMonth = 1;
    public const int MaximumReportingMonth = 12;
    public const int MinimumReportingYear = 2000;
    public const int MaximumReportingYear = 2100;
}

public class FacilityReportingPlanRequest
{
    public string? FacilityId { get; set; }

    /// <summary>
    /// The NHSN measure the facility is enrolled in. Optional when MeasureMappingId is supplied,
    /// which is where it is taken from; required otherwise.
    /// </summary>
    public string? Measure { get; set; }

    /// <summary>
    /// The measure mapping the plan reports against. Omit for an enrollment Link has no mapping
    /// for yet - the measure is still recorded, and an admin maps it afterwards.
    /// </summary>
    public string? MeasureMappingId { get; set; }

    /// <summary>
    /// The NHSN component - MSC or PS. Defaults to MSC when omitted.
    /// </summary>
    public string? Component { get; set; }

    [Range(ReportingPeriodLimits.MinimumReportingMonth, ReportingPeriodLimits.MaximumReportingMonth)]
    public int ReportingMonth { get; set; }

    [Range(ReportingPeriodLimits.MinimumReportingYear, ReportingPeriodLimits.MaximumReportingYear)]
    public int ReportingYear { get; set; }

    public bool IsReporting { get; set; }
}

public class FacilityReportingPlanUpdateRequest : FacilityReportingPlanRequest
{
    [Required]
    public string? Id { get; set; }
}
