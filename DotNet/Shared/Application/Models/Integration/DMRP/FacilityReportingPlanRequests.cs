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

    /// <summary>
    /// The NHSN measure the facility is enrolled in. Optional when MeasureMappingId is supplied,
    /// which is where it is taken from; required otherwise.
    /// </summary>
    [DataMember]
    public string? Measure { get; set; }

    /// <summary>
    /// The measure mapping the plan reports against. Omit for an enrollment Link has no mapping
    /// for yet - the measure is still recorded, and an admin maps it afterwards.
    /// </summary>
    [DataMember]
    public string? MeasureMappingId { get; set; }

    /// <summary>
    /// The NHSN component - MSC or PS. Defaults to MSC when omitted.
    /// </summary>
    [DataMember]
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
