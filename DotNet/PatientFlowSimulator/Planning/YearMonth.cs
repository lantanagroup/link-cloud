using System.Globalization;

namespace LantanaGroup.Link.PatientFlowSimulator.Planning;

/// <summary>
/// A calendar month. Every generated stay belongs to the month it was admitted in, in the facility's
/// own time zone.
/// </summary>
public readonly record struct YearMonth(int Year, int Month) : IComparable<YearMonth>
{
    /// <summary>
    /// First instant of the month as an unzoned local time.
    /// </summary>
    public DateTime LocalStart => new(Year, Month, 1, 0, 0, 0, DateTimeKind.Unspecified);

    /// <summary>
    /// First instant of the next month as an unzoned local time.
    /// </summary>
    public DateTime LocalEndExclusive => LocalStart.AddMonths(1);

    /// <summary>
    /// Number of days in the month.
    /// </summary>
    public int Days => DateTime.DaysInMonth(Year, Month);

    /// <summary>
    /// Parses a month written as yyyy-MM.
    /// </summary>
    public static YearMonth Parse(string text)
    {
        if (!DateTime.TryParseExact(text, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            throw new FormatException($"'{text}' is not a month in yyyy-MM form.");
        }

        return new YearMonth(parsed.Year, parsed.Month);
    }

    /// <summary>
    /// The month a local date-time falls in.
    /// </summary>
    public static YearMonth Of(DateTime local) => new(local.Year, local.Month);

    /// <summary>
    /// The month a zoned date-time falls in, in its own offset.
    /// </summary>
    public static YearMonth Of(DateTimeOffset zoned) => new(zoned.Year, zoned.Month);

    /// <summary>
    /// The month a number of months before or after this one.
    /// </summary>
    public YearMonth AddMonths(int months)
    {
        var moved = LocalStart.AddMonths(months);
        return new YearMonth(moved.Year, moved.Month);
    }

    /// <inheritdoc />
    public int CompareTo(YearMonth other)
    {
        if (Year != other.Year)
        {
            return Year.CompareTo(other.Year);
        }

        return Month.CompareTo(other.Month);
    }

    /// <inheritdoc />
    public override string ToString() => $"{Year:D4}-{Month:D2}";
}
