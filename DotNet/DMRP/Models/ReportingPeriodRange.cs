namespace LantanaGroup.Link.DMRP.Models
{
    /// <summary>
    /// One reporting period: the month and year a reporting plan row is filed for.
    /// </summary>
    /// <param name="Year">Four-digit reporting year.</param>
    /// <param name="Month">Month of the reporting period, 1-12.</param>
    public readonly record struct ReportingPeriod(int Year, int Month)
    {
        /// <summary>
        /// The period <paramref name="months"/> after this one, rolling the year over as it goes.
        /// </summary>
        /// <remarks>
        /// Done through <see cref="DateOnly"/> rather than by hand: month arithmetic that crosses a
        /// year boundary is where the off-by-one lives, and the framework already gets it right. A
        /// month outside 1-12 throws here rather than producing a period that cannot exist.
        /// </remarks>
        public ReportingPeriod AddMonths(int months)
        {
            var shifted = new DateOnly(Year, Month, 1).AddMonths(months);

            return new ReportingPeriod(shifted.Year, shifted.Month);
        }
    }

    /// <summary>
    /// An inclusive span of reporting periods, used to read a facility's plan for a window rather
    /// than for one exact month.
    /// </summary>
    /// <remarks>
    /// Both ends are inclusive, which is what the facility-facing look-ahead means by "the next six
    /// months": the current period counts as one of them.
    /// </remarks>
    /// <param name="From">First period in the window.</param>
    /// <param name="To">Last period in the window.</param>
    public readonly record struct ReportingPeriodRange(ReportingPeriod From, ReportingPeriod To)
    {
        /// <summary>
        /// The window of <paramref name="monthsAhead"/> periods starting at <paramref name="anchor"/>,
        /// counting the anchor itself. <c>monthsAhead: 1</c> is the anchor period alone.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="monthsAhead"/> is less than 1. A window of no periods is a caller mistake,
        /// not a request for an empty result.
        /// </exception>
        public static ReportingPeriodRange LookAhead(ReportingPeriod anchor, int monthsAhead)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(monthsAhead, 1);

            return new ReportingPeriodRange(anchor, anchor.AddMonths(monthsAhead - 1));
        }
    }
}
