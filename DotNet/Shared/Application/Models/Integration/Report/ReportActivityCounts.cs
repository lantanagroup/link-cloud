using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Shared.Application.Models.Integration.Report;

/// <summary>
/// Schedule aggregates for the dashboard. Status mapping, from <c>ScheduleStatus</c>:
/// in flight is New, Scheduled, and EndOfPeriod; submitted is Submitted;
/// not submitted is CompletedNotSubmitted. ScheduleStatus has no failed member.
/// <see cref="ReportActivityCounts.Failed"/> counts rows whose stored status is outside that enum,
/// which is zero for every value the enum defines. Patient-entry
/// FailedValidation and FailedSubmission are a different grain and are not included.
/// Days are UTC calendar days.
/// </summary>
public sealed class ReportActivityCountRequest
{
    public int Days { get; set; } = AggregateCountLimits.DefaultDays;

    /// <summary>When set, only these facilities. Mutually exclusive with <see cref="ExcludeFacilityIds"/>.</summary>
    public List<string>? FacilityIds { get; set; }

    /// <summary>When set, every facility except these. Mutually exclusive with <see cref="FacilityIds"/>.</summary>
    public List<string>? ExcludeFacilityIds { get; set; }
}

public sealed class ReportActivityCounts
{
    public long InFlight { get; set; }
    public long Submitted { get; set; }
    public long NotSubmitted { get; set; }
    public long Failed { get; set; }
    public DateTime? OldestInFlightUtc { get; set; }
    public List<ReportDayCount> CreatedPerDay { get; set; } = [];
}

public sealed class ReportDayCount
{
    public string Day { get; set; } = "";
    public long Count { get; set; }
}
