using FluentAssertions;
using LantanaGroup.Link.DMRP.Scheduling;
using LantanaGroup.Link.Shared.Application.Utilities;

namespace UnitTests.DMRP;

[Trait("Category", "UnitTests")]
public class ReportingPeriodsTests
{
    private static readonly TimeZoneInfo Minus5 =
        TimeZoneInfo.CreateCustomTimeZone("UTC-5", TimeSpan.FromHours(-5), "UTC-5", "UTC-5");

    [Fact]
    public void Coming_midnight_is_the_start_of_the_day_after_the_scheduled_fire_in_the_zone()
    {
        // 23:59 on Oct 31 in UTC-5 is 04:59 UTC on Nov 1.
        var scheduled = new DateTimeOffset(2026, 11, 1, 4, 59, 0, TimeSpan.Zero);

        ReportingPeriods.ComingMidnight(scheduled, Minus5).Should().Be(new DateTime(2026, 11, 1));
    }

    [Fact]
    public void A_fire_recovered_after_midnight_still_anchors_on_its_scheduled_time()
    {
        // The scheduled time is what is passed, so recovery time is irrelevant by construction.
        var scheduled = new DateTimeOffset(2026, 10, 15, 4, 59, 0, TimeSpan.Zero);

        ReportingPeriods.ComingMidnight(scheduled, Minus5).Should().Be(new DateTime(2026, 10, 15));
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
