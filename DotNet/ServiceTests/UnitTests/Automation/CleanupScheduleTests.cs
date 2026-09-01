using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Helpers;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class CleanupScheduleTests
{
    [Fact]
    public void NextDaily_before_slot_is_today()
    {
        var now = DateTimeOffset.Parse("2026-09-01T09:00:00Z");
        CleanupSchedule.NextDailyUtc(now, new TimeOnly(10, 0))
            .Should().Be(DateTimeOffset.Parse("2026-09-01T10:00:00Z"));
    }

    [Fact]
    public void NextDaily_after_slot_is_tomorrow()
    {
        var now = DateTimeOffset.Parse("2026-09-01T10:00:00Z");
        CleanupSchedule.NextDailyUtc(now, new TimeOnly(10, 0))
            .Should().Be(DateTimeOffset.Parse("2026-09-02T10:00:00Z"));
    }

    [Fact]
    public void NextWeekly_sunday_before_slot_is_today()
    {
        var now = DateTimeOffset.Parse("2026-09-06T09:00:00Z"); // Sunday
        CleanupSchedule.NextWeeklyUtc(now, DayOfWeek.Sunday, new TimeOnly(10, 0))
            .Should().Be(DateTimeOffset.Parse("2026-09-06T10:00:00Z"));
    }

    [Fact]
    public void NextWeekly_monday_is_coming_sunday()
    {
        var now = DateTimeOffset.Parse("2026-09-07T12:00:00Z"); // Monday
        CleanupSchedule.NextWeeklyUtc(now, DayOfWeek.Sunday, new TimeOnly(10, 0))
            .Should().Be(DateTimeOffset.Parse("2026-09-13T10:00:00Z"));
    }

    [Fact]
    public void Daily_is_due_inside_catch_up_window()
    {
        var now = DateTimeOffset.Parse("2026-09-01T11:30:00Z");
        CleanupSchedule.IsDueDaily(now, new TimeOnly(10, 0), TimeSpan.FromHours(3), lastRunUtc: null)
            .Should().BeTrue();
    }

    [Fact]
    public void Daily_is_not_due_after_catch_up_window()
    {
        var now = DateTimeOffset.Parse("2026-09-01T14:00:00Z");
        CleanupSchedule.IsDueDaily(now, new TimeOnly(10, 0), TimeSpan.FromHours(3), lastRunUtc: null)
            .Should().BeFalse();
    }

    [Fact]
    public void Daily_is_not_due_when_already_run_today()
    {
        var now = DateTimeOffset.Parse("2026-09-01T11:00:00Z");
        var last = DateTimeOffset.Parse("2026-09-01T10:01:00Z");
        CleanupSchedule.IsDueDaily(now, new TimeOnly(10, 0), TimeSpan.FromHours(3), last)
            .Should().BeFalse();
    }

    [Fact]
    public void Weekly_is_not_due_on_weekday()
    {
        var now = DateTimeOffset.Parse("2026-09-01T10:30:00Z"); // Tuesday
        CleanupSchedule.IsDueWeekly(now, DayOfWeek.Sunday, new TimeOnly(10, 0), TimeSpan.FromHours(3), lastRunUtc: null)
            .Should().BeFalse();
    }
}
