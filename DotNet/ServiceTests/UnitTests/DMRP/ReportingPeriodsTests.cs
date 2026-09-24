using FluentAssertions;
using LantanaGroup.Link.DMRP.Scheduling;
using LantanaGroup.Link.Shared.Application.Utilities;

namespace UnitTests.DMRP;

[Trait("Category", "UnitTests")]
public class ReportingPeriodsTests
{
    private static readonly TimeZoneInfo Minus5 =
        TimeZoneInfo.CreateCustomTimeZone("UTC-5", TimeSpan.FromHours(-5), "UTC-5", "UTC-5");

    private static readonly TimeSpan Nominal2359 = new(23, 59, 0);

    [Fact]
    public void Coming_midnight_is_the_start_of_the_day_after_the_scheduled_fire_in_the_zone()
    {
        // 23:59 on Oct 31 in UTC-5 is 04:59 UTC on Nov 1.
        var scheduled = new DateTimeOffset(2026, 11, 1, 4, 59, 0, TimeSpan.Zero);

        ReportingPeriods.ComingMidnight(scheduled, Minus5, Nominal2359).Should().Be(new DateTime(2026, 11, 1));
    }

    /// <summary>
    /// Quartz's FireOnceNow misfire handling moves a recovered fire's scheduled time to the recovery
    /// instant, so the fire time alone cannot say which night was missed. A fire landing earlier in
    /// the local day than the cron would ever fire is a recovery of the previous night, and the
    /// midnight it was meant to announce is the one that has already passed.
    /// </summary>
    [Fact]
    public void A_fire_recovered_after_midnight_anchors_on_the_midnight_that_already_passed()
    {
        // 00:59 local on Nov 1 in UTC-5: a pod that was down over the 23:59 fire on Oct 31.
        var scheduled = new DateTimeOffset(2026, 11, 1, 5, 59, 0, TimeSpan.Zero);

        ReportingPeriods.ComingMidnight(scheduled, Minus5, Nominal2359).Should().Be(new DateTime(2026, 11, 1));
    }

    [Fact]
    public void A_fire_at_its_nominal_time_anchors_on_the_coming_midnight()
    {
        // 23:59 local on Oct 31 in UTC-5: the ordinary, on-time fire.
        var scheduled = new DateTimeOffset(2026, 11, 1, 4, 59, 0, TimeSpan.Zero);

        ReportingPeriods.ComingMidnight(scheduled, Minus5, Nominal2359).Should().Be(new DateTime(2026, 11, 1));
    }

    [Fact]
    public void A_fire_earlier_in_the_evening_than_nominal_also_anchors_on_the_midnight_that_passed()
    {
        // 22:00 local on Nov 1 in UTC-5 is before the nominal 23:59, so the simple rule reads it as a
        // recovery and anchors on Nov 1's midnight rather than Nov 2's. That is the price of the rule
        // being one comparison; it costs nothing in practice because the cron never fires early - only
        // a hand-triggered or misfire-recovered run can land here.
        var scheduled = new DateTimeOffset(2026, 11, 2, 3, 0, 0, TimeSpan.Zero);

        ReportingPeriods.ComingMidnight(scheduled, Minus5, Nominal2359).Should().Be(new DateTime(2026, 11, 1));
    }

    [Fact]
    public void Without_a_nominal_time_every_fire_anchors_on_the_next_midnight()
    {
        // A cron that fires at more than one time of day names no nominal time, so there is nothing to
        // compare against and the fire is taken at face value.
        var scheduled = new DateTimeOffset(2026, 11, 1, 5, 59, 0, TimeSpan.Zero);

        ReportingPeriods.ComingMidnight(scheduled, Minus5, null).Should().Be(new DateTime(2026, 11, 2));
    }

    [Fact]
    public void Coming_midnight_refuses_a_missing_timezone()
    {
        var act = () => ReportingPeriods.ComingMidnight(DateTimeOffset.UtcNow, null!, Nominal2359);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void A_day_mid_month_starts_only_a_daily_period()
    {
        var periods = ReportingPeriods.StartingAt(new DateTime(2026, 10, 14));

        periods.Should().ContainSingle().Which.Should().Be(
            new ScheduledPeriod(ReportingPeriodMath.Daily, new DateTime(2026, 10, 14)));
    }

    [Fact]
    public void A_sunday_mid_month_is_still_only_a_daily_period()
    {
        // 2026-10-11 is a Sunday. The nightly job never announces weekly periods.
        var periods = ReportingPeriods.StartingAt(new DateTime(2026, 10, 11));

        periods.Select(p => p.Frequency).Should().BeEquivalentTo([ReportingPeriodMath.Daily]);
    }

    [Fact]
    public void The_first_of_a_month_starts_daily_and_monthly()
    {
        var periods = ReportingPeriods.StartingAt(new DateTime(2026, 11, 1));

        periods.Select(p => p.Frequency).Should().BeEquivalentTo(
            [ReportingPeriodMath.Daily, ReportingPeriodMath.Monthly]);
        periods.Should().OnlyContain(p => p.LocalStart == new DateTime(2026, 11, 1));
    }

    [Fact]
    public void A_time_that_is_not_midnight_is_refused()
    {
        var act = () => ReportingPeriods.StartingAt(new DateTime(2026, 10, 1, 23, 59, 0));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void The_late_period_after_a_backfill_is_the_month_already_begun()
    {
        // Coming midnight Nov 4: November started three days ago.
        var late = ReportingPeriods.LateStartingBefore(new DateTime(2026, 11, 4));

        late.Should().ContainSingle().Which.Should().Be(
            new ScheduledPeriod(ReportingPeriodMath.Monthly, new DateTime(2026, 11, 1)));
    }

    [Fact]
    public void There_is_no_late_period_when_the_month_is_only_starting()
    {
        ReportingPeriods.LateStartingBefore(new DateTime(2026, 11, 1)).Should().BeEmpty();
    }
}
