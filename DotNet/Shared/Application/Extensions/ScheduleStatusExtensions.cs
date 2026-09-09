using LantanaGroup.Link.Shared.Application.Enums;

namespace LantanaGroup.Link.Shared.Application.Extensions;


public static class ScheduleStatusExtensions
{
    /// <summary>
    /// Returns true if the schedule status is terminal (i.e. no further processing is expected)
    /// </summary>
    /// <param name="status">ScheduleStatus to check</param>
    /// <returns>True if the schedule status is terminal</returns>
    public static bool IsTerminal(this ScheduleStatus status)
    {
        return status == ScheduleStatus.CompletedNotSubmitted ||
               status == ScheduleStatus.Submitted;
    }
}