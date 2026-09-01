using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;

public static class ReportingPeriodLimits
{
    public const int MinimumReportingMonth = 1;
    public const int MaximumReportingMonth = 12;
    public const int MinimumReportingYear = 2000;
    public const int MaximumReportingYear = 2100;
}

[DataContract]
public class FacilityReportingPlanRequest
{
    [DataMember]
    public string? FacilityId { get; set; }

    [DataMember]
    public string? MeasureMappingId { get; set; }

    /// <summary>
    /// The NHSN component - MSC or PS. Defaults to MSC when omitted.
    /// </summary>
    public string? Component { get; set; }

    [Range(ReportingPeriodLimits.MinimumReportingMonth, ReportingPeriodLimits.MaximumReportingMonth)]
    [DataMember]
    public int ReportingMonth { get; set; }

    [Range(ReportingPeriodLimits.MinimumReportingYear, ReportingPeriodLimits.MaximumReportingYear)]
    [DataMember]
    public int ReportingYear { get; set; }

    [DataMember]
    public bool IsReporting { get; set; }
}

[DataContract]
public class FacilityReportingPlanUpdateRequest : FacilityReportingPlanRequest
{
    [Required]
    [DataMember]
    public string? Id { get; set; }
}
