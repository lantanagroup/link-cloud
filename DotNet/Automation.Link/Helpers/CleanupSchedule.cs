namespace LantanaGroup.Link.Automation.Link.Helpers;

/// <summary>
/// Next-fire and catch-up window for off-hours Automation leftover cleanup.
/// Times are UTC so daily 10:00 UTC stays before US hospital prime hours.
/// </summary>
public static class CleanupSchedule
{
    public static DateTimeOffset NextDailyUtc(DateTimeOffset nowUtc, TimeOnly timeUtc)
    {
        nowUtc = nowUtc.ToUniversalTime();
        var todaySlot = SlotOn(nowUtc.UtcDateTime.Date, timeUtc);
        return nowUtc < todaySlot ? todaySlot : todaySlot.AddDays(1);
    }

    public static DateTimeOffset NextWeeklyUtc(DateTimeOffset nowUtc, DayOfWeek day, TimeOnly timeUtc)
    {
        nowUtc = nowUtc.ToUniversalTime();
        var date = nowUtc.UtcDateTime.Date;
        var daysUntil = ((int)day - (int)date.DayOfWeek + 7) % 7;
        var candidate = SlotOn(date.AddDays(daysUntil), timeUtc);
        return nowUtc < candidate ? candidate : candidate.AddDays(7);
    }

    /// <summary>
    /// True when <paramref name="nowUtc"/> is inside today's scheduled slot plus the
    /// catch-up window, and the job has not already succeeded in that slot.
    /// Missed windows (process down past catch-up) wait until the next scheduled day
    /// so teardown does not run in the middle of business hours.
    /// </summary>
    public static bool IsDueDaily(
        DateTimeOffset nowUtc,
        TimeOnly timeUtc,
        TimeSpan catchUpWindow,
        DateTimeOffset? lastRunUtc)
    {
        nowUtc = nowUtc.ToUniversalTime();
        var todaySlot = SlotOn(nowUtc.UtcDateTime.Date, timeUtc);
        if (nowUtc < todaySlot || nowUtc >= todaySlot + catchUpWindow)
            return false;

        return lastRunUtc is not DateTimeOffset last || last.ToUniversalTime() < todaySlot;
    }

    public static bool IsDueWeekly(
        DateTimeOffset nowUtc,
        DayOfWeek day,
        TimeOnly timeUtc,
        TimeSpan catchUpWindow,
        DateTimeOffset? lastRunUtc)
    {
        nowUtc = nowUtc.ToUniversalTime();
        if (nowUtc.DayOfWeek != day)
            return false;

        return IsDueDaily(nowUtc, timeUtc, catchUpWindow, lastRunUtc);
    }

    public static TimeOnly ParseTimeUtc(string? value, TimeOnly fallback)
        => TimeOnly.TryParse(value, out var parsed) ? parsed : fallback;

    private static DateTimeOffset SlotOn(DateTime utcDate, TimeOnly timeUtc)
        => new(DateTime.SpecifyKind(utcDate.Add(timeUtc.ToTimeSpan()), DateTimeKind.Utc));
}
