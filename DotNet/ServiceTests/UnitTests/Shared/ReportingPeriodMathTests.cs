using FluentAssertions;
using LantanaGroup.Link.Shared.Application.Utilities;

namespace UnitTests.Shared;

[Trait("Category", "UnitTests")]
public class ReportingPeriodMathTests
{
    // A fixed offset with no DST keeps every expected value hand-checkable.
    private static readonly TimeZoneInfo Minus5 =
        TimeZoneInfo.CreateCustomTimeZone("UTC-5", TimeSpan.FromHours(-5), "UTC-5", "UTC-5");

    [Fact]
    public void Monthly_covers_the_calendar_month_of_the_local_date()
    {
        var (start, end) = ReportingPeriodMath.ForFrequency(ReportingPeriodMath.Monthly,
            new DateTime(2026, 10, 15, 13, 45, 0), Minus5);

        start.Should().Be(new DateTime(2026, 10, 1, 5, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2026, 11, 1, 4, 59, 59, DateTimeKind.Utc));
    }

    [Fact]
    public void Weekly_starts_on_the_preceding_sunday()
    {
        // 2026-10-14 is a Wednesday; the week began Sunday 2026-10-11.
        var (start, end) = ReportingPeriodMath.ForFrequency(ReportingPeriodMath.Weekly,
            new DateTime(2026, 10, 14), Minus5);

        start.Should().Be(new DateTime(2026, 10, 11, 5, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2026, 10, 18, 4, 59, 59, DateTimeKind.Utc));
    }

    [Fact]
    public void Weekly_on_a_sunday_starts_that_day()
    {
        var (start, _) = ReportingPeriodMath.ForFrequency(ReportingPeriodMath.Weekly,
            new DateTime(2026, 10, 11), Minus5);

        start.Should().Be(new DateTime(2026, 10, 11, 5, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Daily_covers_the_local_day()
    {
        var (start, end) = ReportingPeriodMath.ForFrequency(ReportingPeriodMath.Daily,
            new DateTime(2026, 10, 14, 23, 59, 0), Minus5);

        start.Should().Be(new DateTime(2026, 10, 14, 5, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2026, 10, 15, 4, 59, 59, DateTimeKind.Utc));
    }

    // The zone every one of these periods is really computed in: the fixed offset above cannot show
    // what a DST boundary does to the length of a period.
    private static readonly TimeZoneInfo Chicago = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");

    /// <summary>
    /// The day the clocks go back is 25 hours long. The period has to cover all of it, or the last
    /// hour of the day is never reported on.
    /// </summary>
    [Fact]
    public void Daily_covers_the_whole_of_a_twenty_five_hour_day()
    {
        var (start, end) = ReportingPeriodMath.ForFrequency(ReportingPeriodMath.Daily,
            new DateTime(2026, 11, 1), Chicago);

        // Nov 1 2026 begins at 00:00 CDT (UTC-5) and ends at 23:59:59 CST (UTC-6).
        start.Should().Be(new DateTime(2026, 11, 1, 5, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2026, 11, 2, 5, 59, 59, DateTimeKind.Utc));
        (end - start).Should().Be(TimeSpan.FromHours(25).Subtract(TimeSpan.FromSeconds(1)));
    }

    /// <summary>The day the clocks go forward is 23 hours long, and the period must not claim 24.</summary>
    [Fact]
    public void Daily_covers_the_whole_of_a_twenty_three_hour_day()
    {
        var (start, end) = ReportingPeriodMath.ForFrequency(ReportingPeriodMath.Daily,
            new DateTime(2027, 3, 14), Chicago);

        // Mar 14 2027 begins at 00:00 CST (UTC-6) and ends at 23:59:59 CDT (UTC-5).
        start.Should().Be(new DateTime(2027, 3, 14, 6, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2027, 3, 15, 4, 59, 59, DateTimeKind.Utc));
        (end - start).Should().Be(TimeSpan.FromHours(23).Subtract(TimeSpan.FromSeconds(1)));
    }

    /// <summary>A month containing a DST change starts and ends on different offsets.</summary>
    [Fact]
    public void Monthly_spans_a_month_that_changes_offset_part_way_through()
    {
        var (start, end) = ReportingPeriodMath.ForFrequency(ReportingPeriodMath.Monthly,
            new DateTime(2026, 11, 15, 9, 0, 0), Chicago);

        start.Should().Be(new DateTime(2026, 11, 1, 5, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2026, 12, 1, 5, 59, 59, DateTimeKind.Utc));
    }

    [Fact]
    public void An_unknown_frequency_is_refused()
    {
        var act = () => ReportingPeriodMath.ForFrequency("Hourly", new DateTime(2026, 10, 14), Minus5);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // Chile moves its clocks forward at local midnight when DST begins, so that night's 00:00 does
    // not exist. A Daily or Monthly period whose local boundary lands there must not throw - it
    // should begin at the first valid instant after the gap instead.
    private static readonly TimeZoneInfo Santiago = TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");

    /// <summary>
    /// The first September 2026 date on which Santiago local midnight falls inside the DST-start
    /// gap. Computed from the zone itself rather than hardcoded, since the exact transition date is
    /// set by Chilean law and this test should not assume it without checking.
    /// </summary>
    private static DateTime FindSeptemberDstGapStart(TimeZoneInfo tz, int year)
    {
        for (var day = 1; day <= 30; day++)
        {
            var candidate = new DateTime(year, 9, day, 0, 0, 0);
            if (tz.IsInvalidTime(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            $"No September {year} DST-start gap found for {tz.Id}; the test's assumption about this zone no longer holds.");
    }

    [Fact]
    public void Daily_period_starting_in_a_DST_gap_begins_at_the_first_valid_local_instant()
    {
        var gapDate = FindSeptemberDstGapStart(Santiago, 2026);
        Santiago.IsInvalidTime(gapDate).Should().BeTrue("the test assumes local midnight itself is inside the gap");

        var act = () => ReportingPeriodMath.ForFrequency(ReportingPeriodMath.Daily, gapDate, Santiago);
        act.Should().NotThrow();

        var (start, end) = ReportingPeriodMath.ForFrequency(ReportingPeriodMath.Daily, gapDate, Santiago);

        // The gap is one hour; the first valid local instant is 01:00.
        start.Should().Be(TimeZoneInfo.ConvertTimeToUtc(gapDate.AddHours(1), Santiago));
        end.Should().Be(TimeZoneInfo.ConvertTimeToUtc(gapDate.AddDays(1).AddSeconds(-1), Santiago));
    }

    [Fact]
    public void Monthly_period_in_the_gap_month_does_not_throw()
    {
        var gapDate = FindSeptemberDstGapStart(Santiago, 2026);

        var act = () => ReportingPeriodMath.ForFrequency(ReportingPeriodMath.Monthly, gapDate, Santiago);
        act.Should().NotThrow();

        var (start, end) = ReportingPeriodMath.ForFrequency(ReportingPeriodMath.Monthly, gapDate, Santiago);

        var monthStart = new DateTime(gapDate.Year, gapDate.Month, 1, 0, 0, 0);
        start.Should().Be(TimeZoneInfo.ConvertTimeToUtc(monthStart, Santiago));
        end.Should().Be(TimeZoneInfo.ConvertTimeToUtc(monthStart.AddMonths(1).AddSeconds(-1), Santiago));
    }

    /// <summary>Control: an ordinary day in the same DST-gap zone is unaffected by the fix.</summary>
    [Fact]
    public void Daily_period_on_an_ordinary_day_in_the_gap_zone_is_unaffected()
    {
        var gapDate = FindSeptemberDstGapStart(Santiago, 2026);
        var normalDate = gapDate.AddDays(-1);
        Santiago.IsInvalidTime(normalDate).Should().BeFalse("this date must be an ordinary day for the control to be meaningful");

        var (start, end) = ReportingPeriodMath.ForFrequency(ReportingPeriodMath.Daily, normalDate, Santiago);

        start.Should().Be(TimeZoneInfo.ConvertTimeToUtc(normalDate, Santiago));
        end.Should().Be(TimeZoneInfo.ConvertTimeToUtc(normalDate.AddDays(1).AddSeconds(-1), Santiago));
    }

    [Fact]
    public void ToUtcAfterGap_converts_a_valid_local_time_in_the_zones_offset()
    {
        ReportingPeriodMath.ToUtcAfterGap(new DateTime(2026, 9, 30, 23, 59, 0), Minus5)
            .Should().Be(new DateTime(2026, 10, 1, 4, 59, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ToUtcAfterGap_steps_past_a_skipped_local_time()
    {
        // 2026-03-08 02:30 does not exist in Chicago: clocks jump from 02:00 CST to 03:00 CDT.
        var chicago = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");

        ReportingPeriodMath.ToUtcAfterGap(new DateTime(2026, 3, 8, 2, 30, 0), chicago)
            .Should().Be(new DateTime(2026, 3, 8, 8, 0, 0, DateTimeKind.Utc));
    }
}
