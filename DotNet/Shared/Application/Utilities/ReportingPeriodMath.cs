namespace LantanaGroup.Link.Shared.Application.Utilities;

/// <summary>
/// The reporting period a ReportScheduled event names, for a frequency and the local date the
/// period is anchored on. Lifted out of Tenant's ReportScheduledJob so that every producer of
/// ReportScheduled derives the same StartDate and EndDate from the same inputs.
/// </summary>
public static class ReportingPeriodMath
{
    public const string Daily = "Daily";
    public const string Weekly = "Weekly";
    public const string Monthly = "Monthly";

    /// <summary>
    /// Start and end of the period containing <paramref name="localDate"/>, converted to UTC.
    /// Weeks start on Sunday. The end is the last second of the period.
    /// </summary>
    public static (DateTime StartUtc, DateTime EndUtc) ForFrequency(string frequency, DateTime localDate,
        TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        var day = new DateTime(localDate.Year, localDate.Month, localDate.Day, 0, 0, 0);

        DateTime start;
        DateTime end;

        switch (frequency)
        {
            case Monthly:
                start = new DateTime(localDate.Year, localDate.Month, 1, 0, 0, 0);
                end = start.AddMonths(1).AddSeconds(-1);
                break;
            case Weekly:
                start = day.AddDays(-(int)(day.DayOfWeek - DayOfWeek.Sunday));
                end = start.AddDays(7).AddSeconds(-1);
                break;
            case Daily:
                start = day;
                end = day.AddDays(1).AddSeconds(-1);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(frequency), frequency,
                    "Expected Daily, Weekly or Monthly.");
        }

        return (TimeZoneInfo.ConvertTimeToUtc(start, timeZone), TimeZoneInfo.ConvertTimeToUtc(end, timeZone));
    }
}
