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

    [Fact]
    public void An_unknown_frequency_is_refused()
    {
        var act = () => ReportingPeriodMath.ForFrequency("Hourly", new DateTime(2026, 10, 14), Minus5);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
