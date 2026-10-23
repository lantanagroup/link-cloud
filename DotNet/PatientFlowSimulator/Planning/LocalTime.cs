namespace LantanaGroup.Link.PatientFlowSimulator.Planning;

/// <summary>
/// Conversions between a facility's local calendar and the UTC instants the files are keyed by.
/// </summary>
public static class LocalTime
{
    /// <summary>
    /// Resolves an IANA (or Windows) time zone id.
    /// </summary>
    public static TimeZoneInfo Resolve(string timeZoneId) => TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

    /// <summary>
    /// Converts an unzoned local time to UTC. A time inside a spring-forward gap is moved one hour later.
    /// </summary>
    public static DateTimeOffset ToUtc(DateTime local, TimeZoneInfo zone)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(unspecified))
        {
            unspecified = unspecified.AddHours(1);
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(unspecified, zone), TimeSpan.Zero);
    }

    /// <summary>
    /// Converts a UTC instant to the zone's local time.
    /// </summary>
    public static DateTimeOffset ToLocal(DateTimeOffset utc, TimeZoneInfo zone) => TimeZoneInfo.ConvertTime(utc, zone);

    /// <summary>
    /// UTC instant at which the month starts in the zone.
    /// </summary>
    public static DateTimeOffset MonthStartUtc(YearMonth month, TimeZoneInfo zone) => ToUtc(month.LocalStart, zone);

    /// <summary>
    /// UTC instant at which the month ends in the zone (exclusive).
    /// </summary>
    public static DateTimeOffset MonthEndUtc(YearMonth month, TimeZoneInfo zone) => ToUtc(month.LocalEndExclusive, zone);

    /// <summary>
    /// Drops minutes and seconds from a UTC instant.
    /// </summary>
    public static DateTimeOffset FloorToHour(DateTimeOffset utc)
    {
        var inUtc = utc.ToUniversalTime();
        return new DateTimeOffset(inUtc.Year, inUtc.Month, inUtc.Day, inUtc.Hour, 0, 0, TimeSpan.Zero);
    }
}
