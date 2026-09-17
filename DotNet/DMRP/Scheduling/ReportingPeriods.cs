using LantanaGroup.Link.Shared.Application.Utilities;

namespace LantanaGroup.Link.DMRP.Scheduling
{
    /// <summary>One reporting period the nightly job announces, anchored on its local start.</summary>
    public sealed record ScheduledPeriod(string Frequency, DateTime LocalStart);

    /// <summary>
    /// Which reporting periods start at a given local midnight. Every period the nightly job
    /// announces begins at the midnight following its fire, so this is the whole of the decision.
    /// Only Daily and Monthly: the nightly job does not produce weekly periods.
    /// </summary>
    public static class ReportingPeriods
    {
        /// <summary>
        /// The local midnight following the scheduled fire. Derived from the scheduled time rather
        /// than the clock so a fire recovered late still names the periods it was scheduled for.
        /// </summary>
        public static DateTime ComingMidnight(DateTimeOffset scheduledFireUtc, TimeZoneInfo timeZone)
        {
            ArgumentNullException.ThrowIfNull(timeZone);

            return TimeZoneInfo.ConvertTime(scheduledFireUtc, timeZone).Date.AddDays(1);
        }

        /// <summary>Daily always; Monthly when the midnight is the first of a month.</summary>
        public static IReadOnlyList<ScheduledPeriod> StartingAt(DateTime comingMidnight)
        {
            if (comingMidnight.TimeOfDay != TimeSpan.Zero)
            {
                throw new ArgumentException("A reporting period starts at midnight.", nameof(comingMidnight));
            }

            var periods = new List<ScheduledPeriod>
            {
                new(ReportingPeriodMath.Daily, comingMidnight)
            };

            if (comingMidnight.Day == 1)
            {
                periods.Add(new ScheduledPeriod(ReportingPeriodMath.Monthly, comingMidnight));
            }

            return periods;
        }

        /// <summary>
        /// The period of the coming midnight's month that already began: the monthly one, when the
        /// month started before this midnight. Announced when a catch-up refresh lands after the
        /// month began, so a failed month-end refresh costs the days already passed rather than the
        /// whole month. Daily periods already passed are not recovered.
        /// </summary>
        public static IReadOnlyList<ScheduledPeriod> LateStartingBefore(DateTime comingMidnight)
        {
            var monthStart = new DateTime(comingMidnight.Year, comingMidnight.Month, 1);

            return monthStart < comingMidnight
                ? [new ScheduledPeriod(ReportingPeriodMath.Monthly, monthStart)]
                : [];
        }
    }
}
