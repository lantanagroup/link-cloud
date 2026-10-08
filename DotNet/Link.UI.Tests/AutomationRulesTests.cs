using FluentAssertions;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class AutomationRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Status_names_match_the_stored_automation_values()
    {
        AutomationRules.StatusLabel("CollectingMetrics").Should().Be("Collecting");
        AutomationRules.StatusLabel("ReportFinalization").Should().Be("Finalizing");
        AutomationRules.StatusLabel("LiveWindowOpen").Should().Be("Live window");
        AutomationRules.StatusLabel("Succeeded").Should().Be("Succeeded");
        AutomationRules.StatusLabel("NotAStatus").Should().Be("NotAStatus");
        AutomationRules.StatusLabel("  ").Should().Be("Unknown");

        AutomationRules.IsInProgress("Running").Should().BeTrue();
        AutomationRules.IsInProgress("CollectingMetrics").Should().BeTrue();
        AutomationRules.IsInProgress("Queued").Should().BeFalse();
        AutomationRules.IsActiveCard("Queued").Should().BeTrue();
        AutomationRules.IsActiveCard("Succeeded").Should().BeFalse();
    }

    [Fact]
    public void A_run_marks_the_facility_it_created()
    {
        var runId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        AutomationRules.MarksAutomationFacility(runId, runId.ToString(), false).Should().BeTrue();
        AutomationRules.MarksAutomationFacility(runId, "bbbbbbbb-bbbb-cccc-dddd-eeeeeeeeeeee", true).Should().BeTrue();
        AutomationRules.MarksAutomationFacility(runId, "12345", true).Should().BeTrue();
        AutomationRules.MarksAutomationFacility(runId, "bbbbbbbb-bbbb-cccc-dddd-eeeeeeeeeeee", false).Should().BeFalse();
        AutomationRules.MarksAutomationFacility(runId, "  ", true).Should().BeFalse();
    }

    [Fact]
    public void Dashboard_counts_use_the_14_day_window()
    {
        var rows = new[]
        {
            Row("Succeeded", Now.AddHours(-2), Now.AddHours(-2).AddSeconds(90)),
            Row("Failed", Now.AddDays(-1), Now.AddDays(-1).AddSeconds(30)),
            Row("Queued", Now.AddMinutes(-5), null),
            Row("Cancelled", Now.AddDays(-2), Now.AddDays(-2).AddMinutes(1)),
            Row("Running", Now.AddMinutes(-1), null),
            Row("Succeeded", new DateTimeOffset(2026, 9, 23, 16, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 9, 23, 16, 0, 10, TimeSpan.Zero))
        };

        var stats = AutomationRules.BuildStats(rows, Now);

        stats.TotalRuns.Should().Be(6);
        stats.Succeeded.Should().Be(2);
        stats.Failed.Should().Be(1);
        stats.Cancelled.Should().Be(1);
        stats.Running.Should().Be(1);
        stats.Queued.Should().Be(1);
        stats.SuccessRate.Should().Be(66.7);
        stats.AvgDurationSeconds.Should().Be(43.3);
        stats.RunsPerDay.Should().HaveCount(14);
        stats.RunsPerDay[0].Date.Should().Be("2026-09-24");
        stats.RunsPerDay[^1].Date.Should().Be("2026-10-07");
        stats.RunsPerDay[^1].Succeeded.Should().Be(1);
        stats.RunsPerDay[^1].Other.Should().Be(2);
        stats.RunsPerDay.Sum(day => day.Succeeded + day.Failed + day.Cancelled + day.Other).Should().Be(5);
    }

    [Fact]
    public void Paging_stays_inside_the_known_set()
    {
        AutomationRules.NormalizeSortBy("RunName").Should().Be("runName");
        AutomationRules.NormalizeSortBy("drop").Should().Be("createdAt");
        AutomationRules.NormalizePageSize(0).Should().Be(20);
        AutomationRules.NormalizePageSize(500).Should().Be(100);
        AutomationRules.NormalizePageNumber(0).Should().Be(1);
        AutomationRules.TotalPages(0, 20).Should().Be(1);
        AutomationRules.TotalPages(21, 20).Should().Be(2);
        AutomationRules.IsDescending("ASC").Should().BeFalse();
        AutomationRules.FormatDuration(0).Should().Be("—");
        AutomationRules.FormatDuration(90).Should().Be("1:30");
    }

    [Fact]
    public void Finished_run_without_a_stored_span_still_has_a_duration()
    {
        var started = new DateTimeOffset(2026, 10, 7, 20, 0, 0, TimeSpan.Zero);
        var finished = started.AddSeconds(60);
        var row = AutomationRules.ToRow(
            Guid.Parse("e4174381-bde8-4668-af6f-5135d43b2753"),
            "Adhoc Report Test",
            "AdhocReportTest",
            "Succeeded",
            1,
            1,
            false,
            started,
            started,
            finished,
            null,
            "  ",
            null,
            false,
            null,
            null);

        row.Duration.Should().Be("1:00");

        var running = AutomationRules.ToRow(
            Guid.NewGuid(),
            "Sample",
            "Custom",
            "Running",
            1,
            1,
            false,
            started,
            started,
            null,
            null,
            null,
            null,
            false,
            null,
            null);
        running.Duration.Should().BeNull();

        var stored = AutomationRules.ToRow(
            Guid.NewGuid(),
            "Sample",
            "Custom",
            "Failed",
            1,
            1,
            false,
            started,
            started,
            finished,
            null,
            "2m 5s",
            null,
            false,
            null,
            null);
        stored.Duration.Should().Be("2m 5s");

        AutomationRules.ResolveDuration(null, "Cancelled", started, started, started).Should().Be("0:00");
    }

    private static Link.UI.Models.AutomationRunRow Row(string status, DateTimeOffset created, DateTimeOffset? finished) =>
        AutomationRules.ToRow(
            Guid.NewGuid(),
            "Sample",
            "Custom",
            status,
            2,
            7,
            false,
            created,
            created,
            finished,
            null,
            null,
            null,
            false,
            null,
            null);
}
