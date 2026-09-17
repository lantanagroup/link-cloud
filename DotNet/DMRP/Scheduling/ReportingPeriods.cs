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
        /// The local midnight the fire announces: normally the one following it, and the one it has
        /// already passed when the fire is a late recovery of the previous night's.
        /// </summary>
        /// <remarks>
        /// The trigger's misfire policy is FireOnceNow, which Quartz implements by moving the
        /// trigger's next fire time to the recovery instant - so on a recovered fire the scheduled
        /// time is when the pod came back, not the 23:59 that was missed, and taking the day after it
        /// would skip a night and announce the wrong periods. <paramref name="nominalLocalTime"/> is
        /// the time of day the cron actually fires at: a fire landing earlier in the local day than
        /// that can only be a recovery, and the midnight it was meant to announce is the one that has
        /// just passed. Null when the cron fires at more than one time of day, which leaves nothing to
        /// compare and the fire taken at face value.
        /// </remarks>
        public static DateTime ComingMidnight(DateTimeOffset scheduledFireUtc, TimeZoneInfo timeZone,
            TimeSpan? nominalLocalTime)
        {
            ArgumentNullException.ThrowIfNull(timeZone);

            var local = TimeZoneInfo.ConvertTime(scheduledFireUtc, timeZone);

            return nominalLocalTime is { } nominal && local.TimeOfDay < nominal
                ? local.Date
                : local.Date.AddDays(1);
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
