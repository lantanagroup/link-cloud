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
    public void MarkProgress_from_paging_logs_is_recent_progress()
    {
        var tracker = new AcquisitionActivityTracker();
        var t0 = DateTime.UtcNow;
        tracker.Observe(12, 10, 1, 1, 0, 0, 7751, t0);

        var t1 = t0.AddMinutes(3);
        tracker.MarkProgress(t1);

        tracker.HasRecentProgress(TimeSpan.FromMinutes(2), t1).Should().BeTrue();
        tracker.HasRecentProgress(TimeSpan.FromMinutes(2), t1.AddMinutes(3)).Should().BeFalse();
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

    [Fact]
    public void Max_extra_duration_still_bounds_keep_alive_when_validation_is_not_running()
    {
        AcquisitionActivityTracker.MaxExtraDuration.Should().Be(TimeSpan.FromHours(6));
    }

    [Fact]
    public void Decide_does_not_time_out_while_validation_is_ongoing_past_the_extra_cap()
    {
        var start = new DateTime(2026, 9, 25, 17, 58, 14, DateTimeKind.Utc);
        var hardTimeout = TimeSpan.FromHours(6);
        var deadline = start + hardTimeout + AcquisitionActivityTracker.MaxExtraDuration + TimeSpan.FromHours(1);
        var now = deadline.AddSeconds(1);

        var decision = AcquisitionActivityTracker.Decide(
            now, start, hardTimeout, deadline, validationOngoing: true, hasRecentProgress: false);

        decision.Continue.Should().BeTrue();
        decision.HeldForValidation.Should().BeTrue();
        decision.Deadline.Should().Be(now + AcquisitionActivityTracker.DeadlineExtension);
    }

    [Fact]
    public void Decide_census_window_miss_does_not_fail_while_validation_work_was_observed()
    {
        // 6010-patient run: keep-alive extended every 5 minutes, then the 2-minute
        // progress window missed the last validation sample and the hard timeout won.
        var start = new DateTime(2026, 9, 25, 17, 58, 14, DateTimeKind.Utc);
        var hardTimeout = TimeSpan.FromSeconds(21600);
        var deadline = start + hardTimeout;
        var now = deadline;

        for (var extension = 0; extension < 11; extension++)
        {
            var decision = AcquisitionActivityTracker.Decide(
                now, start, hardTimeout, deadline, validationOngoing: true, hasRecentProgress: false);

            decision.Continue.Should().BeTrue($"extension {extension} must not time out");
            decision.HeldForValidation.Should().BeTrue();
            deadline = decision.Deadline;
            now = deadline.AddSeconds(1);
        }

        now.Should().BeAfter(start + hardTimeout + TimeSpan.FromMinutes(50));
    }

    [Fact]
    public void Decide_stops_when_the_pipeline_is_idle()
    {
        var start = new DateTime(2026, 9, 25, 17, 58, 14, DateTimeKind.Utc);
        var hardTimeout = TimeSpan.FromHours(6);
        var deadline = start + hardTimeout;

        var decision = AcquisitionActivityTracker.Decide(
            deadline.AddSeconds(1), start, hardTimeout, deadline, validationOngoing: false, hasRecentProgress: false);

        decision.Continue.Should().BeFalse();
        decision.HeldForValidation.Should().BeFalse();
        decision.Deadline.Should().Be(deadline);
    }

    [Fact]
    public void Validation_queue_stays_open_across_a_sample_gap_after_work_is_seen()
    {
        var signal = new ValidationWorkSignal();
        var t0 = new DateTime(2026, 9, 26, 0, 40, 0, DateTimeKind.Utc);

        signal.ObserveCounts(2636, 662, 2711, t0);
        signal.IsOngoingAt(t0).Should().BeFalse();

        signal.ObserveCounts(2634, 662, 2713, t0.AddSeconds(20));
        signal.IsOngoingAt(t0.AddMinutes(10)).Should().BeTrue();
        signal.PendingValidation.Should().Be(2634);

        signal.ObserveCounts(2634, 662, 2713, t0.AddMinutes(10));
        signal.IsOngoingAt(t0.AddMinutes(10)).Should().BeTrue();

        signal.ObserveCounts(0, 3299, 2711, t0.AddMinutes(11));
        signal.IsOngoingAt(t0.AddMinutes(11)).Should().BeFalse();
    }

    [Fact]
    public void Validation_activity_holds_a_single_patient_whose_counts_have_not_moved()
    {
        var signal = new ValidationWorkSignal();
        var t0 = new DateTime(2026, 9, 26, 0, 40, 0, DateTimeKind.Utc);
        signal.ObserveCounts(1, 0, 0, t0);
        signal.NoteActivity(t0);

        signal.IsOngoingAt(t0.AddMinutes(10)).Should().BeTrue();

        signal.ObserveCounts(1, 0, 0, t0.AddMinutes(10));
        signal.IsOngoingAt(t0.AddMinutes(20)).Should().BeTrue();
    }

    [Fact]
    public void Wedged_validation_queue_stops_holding_after_the_quiet_limit()
    {
        var signal = new ValidationWorkSignal();
        var t0 = new DateTime(2026, 9, 26, 0, 40, 0, DateTimeKind.Utc);
        signal.ObserveCounts(2636, 662, 2711, t0);
        signal.ObserveCounts(2634, 662, 2713, t0.AddSeconds(20));

        var wedgedAt = t0.AddSeconds(20).Add(ValidationWorkSignal.QuietLimit).AddSeconds(1);
        signal.IsOngoingAt(wedgedAt).Should().BeFalse();

        var start = new DateTime(2026, 9, 25, 17, 58, 14, DateTimeKind.Utc);
        var hardTimeout = TimeSpan.FromHours(6);
        var deadline = start + hardTimeout + AcquisitionActivityTracker.MaxExtraDuration;
        var decision = AcquisitionActivityTracker.Decide(
            deadline.AddSeconds(1), start, hardTimeout, deadline, validationOngoing: false, hasRecentProgress: false);

        decision.Continue.Should().BeFalse();
    }

    [Fact]
    public void Drained_queue_does_not_carry_the_hold_into_the_next_wave()
    {
        var signal = new ValidationWorkSignal();
        var t0 = new DateTime(2026, 9, 26, 1, 0, 0, DateTimeKind.Utc);
        signal.ObserveCounts(2, 0, 0, t0);
        signal.ObserveCounts(1, 1, 0, t0.AddSeconds(5));
        signal.ObserveCounts(0, 2, 0, t0.AddSeconds(10));

        signal.ObserveCounts(4, 0, 0, t0.AddMinutes(1));
        signal.IsOngoingAt(t0.AddMinutes(1)).Should().BeFalse();
    }

    [Fact]
    public void Empty_entry_read_after_entries_were_seen_is_not_the_queue_draining()
    {
        ValidationWorkSignal.IsTransientEmptyEntryRead(0, 6010).Should().BeTrue();
        ValidationWorkSignal.IsTransientEmptyEntryRead(0, 0).Should().BeFalse();
        ValidationWorkSignal.IsTransientEmptyEntryRead(6010, 6010).Should().BeFalse();
    }
}
