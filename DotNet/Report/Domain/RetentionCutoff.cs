using LantanaGroup.Link.Shared.Application.Models.Retention;

namespace LantanaGroup.Link.Report.Domain;

/// <summary>
/// The retention cutoff: a report whose governing date is strictly earlier is eligible for cleanup. See
/// dev-docs/retention-check.md.
/// </summary>
public static class RetentionCutoff
{
    /// <summary>
    /// The cutoff for <paramref name="window"/> counted back from <paramref name="now"/>, in UTC, with calendar
    /// arithmetic.
    /// </summary>
    /// <remarks>
    /// A month is a calendar month, never a fixed number of days. When the day does not exist in the earlier
    /// month, the cutoff is that month's last day: three months before 31 May is 28 February, or 29 February
    /// in a leap year. The time of day is kept, so the cutoff is exactly one window before the run.
    /// </remarks>
    /// <param name="now">
    /// When the check runs. Converted to UTC first, so the offset it carries does not matter.
    /// </param>
    /// <param name="window">
    /// The retention window, in months or days.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The window's count is less than 1, or its unit is not one the contract defines.
    /// </exception>
    public static DateTimeOffset From(DateTimeOffset now, RetentionWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentOutOfRangeException.ThrowIfLessThan(window.Count, 1, nameof(window));

        var nowUtc = now.ToUniversalTime();

        return window.Unit switch
        {
            RetentionWindowUnit.Months => nowUtc.AddMonths(-window.Count),
            RetentionWindowUnit.Days => nowUtc.AddDays(-window.Count),
            _ => throw new ArgumentOutOfRangeException(nameof(window),
                                                       window.Unit,
                                                       "The retention window unit is not Months or Days.")
        };
    }
}
