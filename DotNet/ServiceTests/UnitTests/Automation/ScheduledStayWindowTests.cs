using LantanaGroup.Automation.Generation;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class ScheduledStayWindowTests
{
    private static readonly DateTime PeriodStart = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PeriodEnd = new(2026, 1, 31, 23, 59, 59, DateTimeKind.Utc);

    [Fact]
    public void Remains_after_starts_before_period_and_ends_after()
    {
        var (start, end) = ScheduledStayWindow.Compute(
            ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod,
            PeriodStart,
            PeriodEnd);

        Assert.True(start < PeriodStart);
        Assert.True(end > PeriodEnd);
    }

    [Fact]
    public void Entirely_before_ends_before_period_start()
    {
        var (_, end) = ScheduledStayWindow.Compute(
            ScheduledInpatientPattern.AdmittedAndDischargedBeforePeriod,
            PeriodStart,
            PeriodEnd);

        Assert.True(end < PeriodStart);
    }

    [Fact]
    public void Entirely_after_starts_after_period_end()
    {
        var (start, _) = ScheduledStayWindow.Compute(
            ScheduledInpatientPattern.AdmittedAndDischargedAfterPeriod,
            PeriodStart,
            PeriodEnd);

        Assert.True(start > PeriodEnd);
    }

    [Fact]
    public void During_period_stay_overlaps_the_window()
    {
        var (start, end) = ScheduledStayWindow.Compute(
            ScheduledInpatientPattern.AdmittedDuringPeriodDischargedDuringPeriod,
            PeriodStart,
            PeriodEnd);

        Assert.True(start >= PeriodStart);
        Assert.True(end <= PeriodEnd);
        Assert.True(start < end);
    }

    [Fact]
    public void Same_seed_is_deterministic()
    {
        var a = ScheduledStayWindow.Compute(
            ScheduledInpatientPattern.AdmittedDuringPeriodRemainsInpatientAfterPeriod,
            PeriodStart,
            PeriodEnd,
            seed: 17);
        var b = ScheduledStayWindow.Compute(
            ScheduledInpatientPattern.AdmittedDuringPeriodRemainsInpatientAfterPeriod,
            PeriodStart,
            PeriodEnd,
            seed: 17);

        Assert.Equal(a.Start, b.Start);
        Assert.Equal(a.End, b.End);
    }
}
