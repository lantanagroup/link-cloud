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
    /// Weeks start on Sunday. The end is the last second of the period. When a period's local
    /// boundary falls inside a DST gap (the clock jumps forward past it, so it never occurs), the
    /// period begins at the first valid local instant after the gap instead of throwing.
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

        return (ToUtcAfterGap(start, timeZone), ToUtcAfterGap(end, timeZone));
    }

    /// <summary>
    /// Converts <paramref name="local"/> to UTC, stepping forward minute by minute first if it falls
    /// inside a DST gap (a local time skipped when the clock jumps forward) so the conversion never
    /// throws. Bounded to <see cref="MaxGapMinutes"/> minutes; a gap wider than that is not a DST
    /// transition this platform knows how to reason about, so it is reported rather than silently
    /// walked past.
    /// </summary>
    private const int MaxGapMinutes = 180;

    public static DateTime ToUtcAfterGap(DateTime local, TimeZoneInfo timeZone)
    {
        if (timeZone.IsInvalidTime(local))
        {
            var adjusted = local;
            var minutesAdvanced = 0;

            while (timeZone.IsInvalidTime(adjusted))
            {
                if (minutesAdvanced >= MaxGapMinutes)
                {
                    throw new InvalidOperationException(
                        $"No valid local time found within {MaxGapMinutes} minutes after {local:O} in time zone " +
                        $"'{timeZone.Id}'; this is wider than any known DST gap, so the period boundary was not advanced past it.");
                }

                adjusted = adjusted.AddMinutes(1);
                minutesAdvanced++;
            }

            local = adjusted;
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
    }
}
