using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Models;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class DashboardStatsAggregatorTests
{
    private static AutomationRunSummary Run(
        AutomationRunStatus status,
        DateTimeOffset? createdAt = null,
        TimeSpan? duration = null,
        Guid? id = null)
    {
        var created = createdAt ?? DateTimeOffset.UtcNow.AddHours(-1);
        return new AutomationRunSummary
        {
            RunId = id ?? Guid.NewGuid(),
            RunName = "test",
            Status = status,
            CreatedAt = created,
            FinishedAt = duration.HasValue ? created.Add(duration.Value) : null,
        };
    }

    private static Mock<ISnapshotStore> StoreReturning(params AutomationRunSummary[] persisted)
    {
        var mock = new Mock<ISnapshotStore>();
        mock.Setup(s => s.GetAllRunSummariesAsync(It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(persisted);
        return mock;
    }

    [Fact]
    public async Task Status_counts_aggregate_persisted_runs()
    {
        var store = StoreReturning(
            Run(AutomationRunStatus.Succeeded),
            Run(AutomationRunStatus.Succeeded),
            Run(AutomationRunStatus.Failed),
            Run(AutomationRunStatus.Cancelled),
            Run(AutomationRunStatus.Running),
            Run(AutomationRunStatus.Queued));

        var stats = await new DashboardStatsAggregator(store.Object).BuildAsync(inMemoryRuns: []);

        stats.TotalRuns.Should().Be(6);
        stats.Succeeded.Should().Be(2);
        stats.Failed.Should().Be(1);
        stats.Cancelled.Should().Be(1);
        stats.Running.Should().Be(1);
        stats.Queued.Should().Be(1);
    }

    [Fact]
    public async Task In_memory_runs_not_yet_persisted_get_merged_in()
    {
        var inMemoryOnly = Run(AutomationRunStatus.Running);
        var store = StoreReturning(/* nothing persisted yet */);

        var stats = await new DashboardStatsAggregator(store.Object)
            .BuildAsync(inMemoryRuns: [inMemoryOnly]);

        stats.TotalRuns.Should().Be(1);
        stats.Running.Should().Be(1);
    }

    [Fact]
    public async Task Live_window_and_finalization_count_as_running()
    {
        var store = StoreReturning(
            Run(AutomationRunStatus.LiveWindowOpen),
            Run(AutomationRunStatus.ReportFinalization),
            Run(AutomationRunStatus.Running));

        var stats = await new DashboardStatsAggregator(store.Object).BuildAsync(inMemoryRuns: []);

        stats.Running.Should().Be(3);
        stats.Queued.Should().Be(0);
    }

    [Fact]
    public async Task Collecting_metrics_counts_as_running_not_succeeded()
    {
        var store = StoreReturning(Run(AutomationRunStatus.CollectingMetrics));

        var stats = await new DashboardStatsAggregator(store.Object).BuildAsync(inMemoryRuns: []);

        stats.Running.Should().Be(1);
        stats.Succeeded.Should().Be(0);
        stats.Failed.Should().Be(0);
    }

    [Fact]
    public async Task Persisted_run_is_not_double_counted_when_also_present_in_memory()
    {
        var sharedId = Guid.NewGuid();
        var persisted = Run(AutomationRunStatus.Succeeded, id: sharedId);
        var inMemorySameId = Run(AutomationRunStatus.Running, id: sharedId);
        var store = StoreReturning(persisted);

        var stats = await new DashboardStatsAggregator(store.Object)
            .BuildAsync(inMemoryRuns: [inMemorySameId]);

        stats.TotalRuns.Should().Be(1, "the persisted version wins; the in-memory copy must be deduped");
        stats.Succeeded.Should().Be(1);
        stats.Running.Should().Be(0);
    }

    [Fact]
    public async Task Avg_duration_uses_only_completed_runs_with_positive_duration()
    {
        var store = StoreReturning(
            Run(AutomationRunStatus.Succeeded, duration: TimeSpan.FromSeconds(60)),
            Run(AutomationRunStatus.Failed,    duration: TimeSpan.FromSeconds(120)),
            Run(AutomationRunStatus.Cancelled, duration: TimeSpan.FromSeconds(999)),  // excluded
            Run(AutomationRunStatus.Running)); // no FinishedAt → excluded

        var stats = await new DashboardStatsAggregator(store.Object).BuildAsync(inMemoryRuns: []);

        stats.AvgDurationSeconds.Should().Be(90.0);
    }

    [Fact]
    public async Task Avg_duration_is_zero_when_no_completed_runs()
    {
        var store = StoreReturning(Run(AutomationRunStatus.Running));

        var stats = await new DashboardStatsAggregator(store.Object).BuildAsync(inMemoryRuns: []);

        stats.AvgDurationSeconds.Should().Be(0);
    }

    [Fact]
    public async Task Histogram_always_seeds_14_day_buckets_even_when_store_is_empty()
    {
        var store = StoreReturning();

        var stats = await new DashboardStatsAggregator(store.Object).BuildAsync(inMemoryRuns: []);

        stats.RunsPerDay.Should().HaveCount(14);
        // Today's bucket must be present.
        stats.RunsPerDay.Should().ContainKey(DateTime.UtcNow.Date.ToString("yyyy-MM-dd"));
        // 13 days ago must be present (oldest bucket in the window).
        stats.RunsPerDay.Should().ContainKey(DateTime.UtcNow.AddDays(-13).Date.ToString("yyyy-MM-dd"));
    }

    [Fact]
    public async Task Histogram_buckets_runs_into_their_created_day()
    {
        var today = DateTimeOffset.UtcNow;
        var twoDaysAgo = today.AddDays(-2);

        var store = StoreReturning(
            Run(AutomationRunStatus.Succeeded, createdAt: today),
            Run(AutomationRunStatus.Failed,    createdAt: today),
            Run(AutomationRunStatus.Succeeded, createdAt: twoDaysAgo));

        var stats = await new DashboardStatsAggregator(store.Object).BuildAsync(inMemoryRuns: []);

        var todayKey = today.Date.ToString("yyyy-MM-dd");
        var twoAgoKey = twoDaysAgo.Date.ToString("yyyy-MM-dd");

        stats.RunsPerDay[todayKey].Succeeded.Should().Be(1);
        stats.RunsPerDay[todayKey].Failed.Should().Be(1);
        stats.RunsPerDay[twoAgoKey].Succeeded.Should().Be(1);
    }

    [Fact]
    public async Task Aggregator_passes_a_14_day_cutoff_to_the_store()
    {
        DateTimeOffset? capturedSince = null;
        var store = new Mock<ISnapshotStore>();
        store.Setup(s => s.GetAllRunSummariesAsync(It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .Callback<DateTimeOffset?, CancellationToken>((since, _) => capturedSince = since)
            .ReturnsAsync(Array.Empty<AutomationRunSummary>());

        await new DashboardStatsAggregator(store.Object).BuildAsync(inMemoryRuns: []);

        capturedSince.Should().NotBeNull();
        var expected = DateTimeOffset.UtcNow.AddDays(-14);
        capturedSince!.Value.Should().BeCloseTo(expected, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Null_in_memory_collection_is_treated_as_empty()
    {
        var store = StoreReturning(Run(AutomationRunStatus.Succeeded));

        var stats = await new DashboardStatsAggregator(store.Object).BuildAsync(inMemoryRuns: null!);

        stats.TotalRuns.Should().Be(1);
    }
}
