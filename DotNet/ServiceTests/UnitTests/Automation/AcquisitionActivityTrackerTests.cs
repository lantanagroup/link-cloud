using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Helpers;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class AcquisitionActivityTrackerTests
{
    [Fact]
    public void First_observe_logs_status_not_keep_alive()
    {
        var tracker = new AcquisitionActivityTracker();
        var now = DateTime.UtcNow;

        var observation = tracker.Observe(12, 10, 1, 1, 0, 0, 7751, now);

        observation.ShouldLogStatus.Should().BeTrue();
        observation.ShouldLogKeepAlive.Should().BeFalse();
        tracker.InFlight.Should().BeTrue();
        tracker.LastResourcesAcquired.Should().Be(7751);
        tracker.HasRecentProgress(TimeSpan.FromMinutes(2), now).Should().BeTrue();
    }

    [Fact]
    public void Resource_growth_without_status_change_emits_keep_alive()
    {
        var tracker = new AcquisitionActivityTracker();
        var t0 = DateTime.UtcNow;
        tracker.Observe(12, 10, 1, 1, 0, 0, 7751, t0);

        var t1 = t0.AddSeconds(10);
        var observation = tracker.Observe(12, 10, 1, 1, 0, 0, 8000, t1);

        observation.ShouldLogStatus.Should().BeFalse();
        observation.ShouldLogKeepAlive.Should().BeTrue();
        observation.ResourceDelta.Should().Be(249);
        observation.ResourcesAcquired.Should().Be(8000);
        tracker.HasRecentProgress(TimeSpan.FromMinutes(2), t1).Should().BeTrue();
    }

    [Fact]
    public void Unchanged_snapshot_is_not_recent_progress_after_window()
    {
        var tracker = new AcquisitionActivityTracker();
        var t0 = DateTime.UtcNow;
        tracker.Observe(12, 10, 1, 1, 0, 0, 7751, t0);
        tracker.Observe(12, 10, 1, 1, 0, 0, 7751, t0.AddSeconds(10));

        tracker.HasRecentProgress(TimeSpan.FromMinutes(2), t0.AddMinutes(3)).Should().BeFalse();
        tracker.InFlight.Should().BeTrue();
    }

    [Fact]
    public void TryExtendDeadline_slides_when_progressing_past_timeout()
    {
        var start = new DateTime(2026, 8, 31, 14, 15, 0, DateTimeKind.Utc);
        var hardTimeout = TimeSpan.FromMinutes(30);
        var deadline = start + hardTimeout;
        var now = deadline.AddSeconds(1);

        var extended = AcquisitionActivityTracker.TryExtendDeadline(
            now, start, hardTimeout, hasRecentProgress: true, ref deadline, out var extendedBy);

        extended.Should().BeTrue();
        extendedBy.Should().Be(AcquisitionActivityTracker.DeadlineExtension);
        deadline.Should().Be(now + AcquisitionActivityTracker.DeadlineExtension);
    }

    [Fact]
    public void TryExtendDeadline_does_not_slide_when_acquisition_is_idle()
    {
        var start = new DateTime(2026, 8, 31, 14, 15, 0, DateTimeKind.Utc);
        var hardTimeout = TimeSpan.FromMinutes(30);
        var deadline = start + hardTimeout;
        var original = deadline;

        var extended = AcquisitionActivityTracker.TryExtendDeadline(
            deadline.AddSeconds(1), start, hardTimeout, hasRecentProgress: false, ref deadline, out _);

        extended.Should().BeFalse();
        deadline.Should().Be(original);
    }

    [Fact]
    public void TryExtendDeadline_caps_total_wait()
    {
        var start = new DateTime(2026, 8, 31, 14, 15, 0, DateTimeKind.Utc);
        var hardTimeout = TimeSpan.FromMinutes(30);
        var deadline = start + hardTimeout + AcquisitionActivityTracker.MaxExtraDuration;
        var now = deadline;

        var extended = AcquisitionActivityTracker.TryExtendDeadline(
            now, start, hardTimeout, hasRecentProgress: true, ref deadline, out _);

        extended.Should().BeFalse();
    }
}
