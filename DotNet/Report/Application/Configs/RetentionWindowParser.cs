using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using LantanaGroup.Link.Shared.Application.Models.Retention;

namespace LantanaGroup.Link.Report.Application.Options;

/// <summary>
/// Reads a retention window written as an ISO-8601 duration, such as <c>P3M</c>, into the
/// <see cref="RetentionWindow"/> the retention messages carry. See dev-docs/retention-check.md.
/// </summary>
/// <remarks>
/// <para>
/// Only one years, months, weeks or days component is accepted. Years become months and weeks become days, so
/// the window keeps the calendar meaning the contract requires: <c>P3M</c> is three calendar months, never a
/// fixed 90 days.
/// </para>
/// <para>
/// Never read the value with <c>XmlConvert.ToTimeSpan</c> or <c>TimeSpan</c>: both turn months into a fixed
/// number of days.
/// </para>
/// </remarks>
public static class RetentionWindowParser
{
    /// <summary>
    /// The longest window allowed in months, from ADR 08.
    /// </summary>
    public const int MaxMonths = 36;

    /// <summary>
    /// The longest window allowed in days: the shortest span 36 months can cover.
    /// </summary>
    public const int MaxDays = 1095;

    // [0-9] rather than \d, which matches any Unicode digit. Nine digits always fit in an int.
    private static readonly Regex Pattern = new(@"^P(?<count>[0-9]{1,9})(?<designator>[YMWD])$",
                                                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// Parses <paramref name="text"/> as a single-component ISO-8601 duration of years, months, weeks or days,
    /// within the 36-month maximum.
    /// </summary>
    /// <param name="text">
    /// The configured value, such as <c>P3M</c>, <c>P1Y</c>, <c>P2W</c> or <c>P90D</c>.
    /// </param>
    /// <param name="window">
    /// The window in months or days, when the value is valid.
    /// </param>
    /// <param name="error">
    /// Why the value is invalid, when it is.
    /// </param>
    /// <returns>
    /// Whether the value is a valid window.
    /// </returns>
    public static bool TryParse(string? text,
                                [NotNullWhen(true)] out RetentionWindow? window,
                                [NotNullWhen(false)] out string? error)
    {
        window = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "is empty. Use an ISO-8601 duration such as P3M.";
            return false;
        }

        var trimmed = text.Trim();
        var match = Pattern.Match(trimmed);
        if (!match.Success)
        {
            error = $"'{trimmed}' is not a single ISO-8601 years, months, weeks or days value " +
                    "such as P3M, P1Y, P2W or P90D.";
            return false;
        }

        var count = long.Parse(match.Groups["count"].Value, CultureInfo.InvariantCulture);
        if (count == 0)
        {
            error = $"'{trimmed}' is zero. The window must be at least one day or month.";
            return false;
        }

        var (unit, converted) = char.ToUpperInvariant(match.Groups["designator"].Value[0]) switch
        {
            'Y' => (RetentionWindowUnit.Months, count * 12),
            'M' => (RetentionWindowUnit.Months, count),
            'W' => (RetentionWindowUnit.Days, count * 7),
            _ => (RetentionWindowUnit.Days, count)
        };

        if (unit == RetentionWindowUnit.Months && converted > MaxMonths)
        {
            error = $"'{trimmed}' is {converted} months. The longest window allowed is {MaxMonths} months.";
            return false;
        }

        if (unit == RetentionWindowUnit.Days && converted > MaxDays)
        {
            error = $"'{trimmed}' is {converted} days. The longest window allowed is {MaxDays} days.";
            return false;
        }

        window = new RetentionWindow
        {
            Unit = unit,
            Count = (int)converted
        };
        error = null;
        return true;
    }
}
