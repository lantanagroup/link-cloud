using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;

/// <summary>
/// Acquisition-log aggregates. A day is a UTC calendar day of <c>ExecutionDate</c>.
/// Failed is <c>RequestStatus.Failed</c> plus <c>RequestStatus.MaxRetriesReached</c>.
/// <see cref="AcquisitionActivityCounts.FailedTotal"/> is those statuses with no day bound.
/// Logs with no execution date are left out of the day series.
/// </summary>
public sealed class AcquisitionActivityCountRequest
{
    public int Days { get; set; } = AggregateCountLimits.DefaultDays;
}

public sealed class AcquisitionActivityCounts
{
    public long FailedTotal { get; set; }
    public List<AcquisitionDayCount> Days { get; set; } = [];
}

public sealed class AcquisitionDayCount
{
    public string Day { get; set; } = "";
    public long Total { get; set; }
    public long Failed { get; set; }
}
