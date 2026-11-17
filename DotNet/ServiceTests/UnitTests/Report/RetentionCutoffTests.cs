using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Shared.Application.Models.Retention;

namespace UnitTests.Report;

/// <summary>
/// Covers the retention cutoff: calendar months and days counted back in UTC.
/// </summary>
[Trait("Category", "UnitTests")]
public class RetentionCutoffTests
{
    private static readonly DateTimeOffset RunTime = new(2026, 11, 6, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void From_ThreeMonths_ReturnsSameDayAndTimeThreeMonthsEarlier()
    {
        var cutoff = RetentionCutoff.From(RunTime, Months(3));

        Assert.Equal(new DateTimeOffset(2026, 8, 6, 2, 0, 0, TimeSpan.Zero), cutoff);
    }

    [Fact]
    public void From_ThirtySixMonths_ReturnsThreeYearsEarlier()
    {
        var cutoff = RetentionCutoff.From(RunTime, Months(36));

        Assert.Equal(new DateTimeOffset(2023, 11, 6, 2, 0, 0, TimeSpan.Zero), cutoff);
    }

    [Theory]
    [InlineData("2026-05-31T02:00:00Z", 3, "2026-02-28T02:00:00Z")]
    [InlineData("2028-05-31T02:00:00Z", 3, "2028-02-29T02:00:00Z")]
    [InlineData("2028-03-31T02:00:00Z", 1, "2028-02-29T02:00:00Z")]
    [InlineData("2028-02-29T02:00:00Z", 12, "2027-02-28T02:00:00Z")]
    [InlineData("2026-12-31T02:00:00Z", 1, "2026-11-30T02:00:00Z")]
    public void From_MonthWithoutThatDay_ReturnsLastDayOfEarlierMonth(string now, int months, string expected)
    {
        var cutoff = RetentionCutoff.From(DateTimeOffset.Parse(now), Months(months));

        Assert.Equal(DateTimeOffset.Parse(expected), cutoff);
    }

    [Theory]
    [InlineData("2026-11-06T02:00:00Z", 90, "2026-08-08T02:00:00Z")]
    [InlineData("2026-03-08T02:00:00Z", 30, "2026-02-06T02:00:00Z")]
    [InlineData("2028-03-01T02:00:00Z", 1, "2028-02-29T02:00:00Z")]
    [InlineData("2026-11-06T02:00:00Z", 1095, "2023-11-07T02:00:00Z")]
    public void From_Days_ReturnsThatManyDaysEarlier(string now, int days, string expected)
    {
        var cutoff = RetentionCutoff.From(DateTimeOffset.Parse(now), Days(days));

        Assert.Equal(DateTimeOffset.Parse(expected), cutoff);
    }

    [Fact]
    public void From_NowWithOffset_ReturnsSameInstantInUtc()
    {
        var eastern = new DateTimeOffset(2026, 11, 5, 21, 0, 0, TimeSpan.FromHours(-5));

        var cutoff = RetentionCutoff.From(eastern, Months(3));

        Assert.Equal(TimeSpan.Zero, cutoff.Offset);
        Assert.Equal(new DateTimeOffset(2026, 8, 6, 2, 0, 0, TimeSpan.Zero), cutoff);
    }

    [Fact]
    public void From_NowWithOffsetAcrossMonthEnd_UsesTheUtcDate()
    {
        // 31 May 22:00 in Hawaii is 1 June 08:00 UTC, so three months back is 1 March, not 28 February.
        var hawaii = new DateTimeOffset(2026, 5, 31, 22, 0, 0, TimeSpan.FromHours(-10));

        var cutoff = RetentionCutoff.From(hawaii, Months(3));

        Assert.Equal(new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero), cutoff);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void From_CountBelowOne_Throws(int count)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RetentionCutoff.From(RunTime, Months(count)));
    }

    [Fact]
    public void From_UnknownUnit_Throws()
    {
        var window = new RetentionWindow
        {
            Unit = (RetentionWindowUnit)99,
            Count = 3
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => RetentionCutoff.From(RunTime, window));
    }

    [Fact]
    public void From_NullWindow_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => RetentionCutoff.From(RunTime, null!));
    }

    private static RetentionWindow Months(int count)
    {
        return new RetentionWindow
        {
            Unit = RetentionWindowUnit.Months,
            Count = count
        };
    }

    private static RetentionWindow Days(int count)
    {
        return new RetentionWindow
        {
            Unit = RetentionWindowUnit.Days,
            Count = count
        };
    }
}
