using Automation.UI.Services.Persistence;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Models;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.AutomationUI;

/// <summary>
/// End-to-end coverage of <see cref="Automation.UI.Services.AutomationRunManager.GetDashboardStatsAsync"/>.
///
/// These tests are the ones the original bug would have been caught by: they drive
/// the same code path the Runs/Index view binds to, against real MongoDB, and
/// validate the totals, per-status counts, 14-day histogram layout, success rate,
/// and average-duration math.
///
/// The store-level tests in <see cref="MongoSnapshotStoreDashboardTests"/> prove
/// the persistence and migration layer behave correctly; these tests prove the
/// aggregation on top of that store is correct.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class AutomationRunManagerDashboardStatsTests : IAsyncLifetime
{
    private readonly AutomationUIIntegrationTestFixture _fixture;

    public AutomationRunManagerDashboardStatsTests(AutomationUIIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Dashboard_includes_all_runs_created_within_the_last_14_days()
    {
        var store = _fixture.CreateStore();

        // One run on each of the last 14 days, all Succeeded.
        var expectedRunIds = new List<Guid>();
        for (var day = 0; day < 14; day++)
        {
            var run = MakeSummary(AutomationRunStatus.Succeeded, DateTimeOffset.UtcNow.AddDays(-day).AddHours(-1));
            expectedRunIds.Add(run.RunId);
            await store.UpsertRunSummaryAsync(run, run.FacilityId, run.ReportId, CancellationToken.None);
        }

        // And one run outside the window that must NOT count.
        var ancient = MakeSummary(AutomationRunStatus.Succeeded, DateTimeOffset.UtcNow.AddDays(-30));
        await store.UpsertRunSummaryAsync(ancient, ancient.FacilityId, ancient.ReportId, CancellationToken.None);

        var manager = _fixture.CreateRunManager();
        var stats = await manager.GetDashboardStatsAsync();

        stats.TotalRuns.Should().Be(14, "runs outside the 14-day window must be excluded");
        stats.Succeeded.Should().Be(14);
    }

    [Fact]
    public async Task Dashboard_status_counts_reflect_mix_of_statuses_within_window()
    {
        var store = _fixture.CreateStore();

        await SeedAsync(store, AutomationRunStatus.Succeeded, days: 1, count: 3);
        await SeedAsync(store, AutomationRunStatus.Failed,    days: 2, count: 2);
        await SeedAsync(store, AutomationRunStatus.Cancelled, days: 5, count: 1);

        var stats = await _fixture.CreateRunManager().GetDashboardStatsAsync();

        stats.TotalRuns.Should().Be(6);
        stats.Succeeded.Should().Be(3);
        stats.Failed.Should().Be(2);
        stats.Cancelled.Should().Be(1);
        stats.Running.Should().Be(0);
        stats.Queued.Should().Be(0);
    }

    [Fact]
    public async Task Dashboard_SuccessRate_uses_only_Succeeded_and_Failed_as_denominator()
    {
        var store = _fixture.CreateStore();

        await SeedAsync(store, AutomationRunStatus.Succeeded, days: 1, count: 4);
        await SeedAsync(store, AutomationRunStatus.Failed,    days: 2, count: 1);
        // Cancelled runs must NOT depress the success rate.
        await SeedAsync(store, AutomationRunStatus.Cancelled, days: 3, count: 10);

        var stats = await _fixture.CreateRunManager().GetDashboardStatsAsync();

        // 4 / (4 + 1) = 80.0
        stats.SuccessRate.Should().Be(80.0);
    }

    [Fact]
    public async Task Dashboard_AvgDurationSeconds_averages_completed_runs_only()
    {
        var store = _fixture.CreateStore();

        // Three completed runs: 60s, 120s, 180s → avg 120s.
        await SeedCompletedAsync(store, AutomationRunStatus.Succeeded, DateTimeOffset.UtcNow.AddDays(-1), durationSeconds: 60);
        await SeedCompletedAsync(store, AutomationRunStatus.Failed,    DateTimeOffset.UtcNow.AddDays(-2), durationSeconds: 120);
        await SeedCompletedAsync(store, AutomationRunStatus.Succeeded, DateTimeOffset.UtcNow.AddDays(-3), durationSeconds: 180);

        // Cancelled run must be excluded from avg duration.
        await SeedCompletedAsync(store, AutomationRunStatus.Cancelled, DateTimeOffset.UtcNow.AddDays(-4), durationSeconds: 9999);

        var stats = await _fixture.CreateRunManager().GetDashboardStatsAsync();

        stats.AvgDurationSeconds.Should().BeApproximately(120.0, precision: 0.5);
    }

    [Fact]
    public async Task Dashboard_RunsPerDay_has_exactly_14_buckets_spanning_today_minus_13_through_today()
    {
        var stats = await _fixture.CreateRunManager().GetDashboardStatsAsync();

        stats.RunsPerDay.Should().HaveCount(14,
            "the histogram pre-seeds one bucket per day for a rolling 14-day window");

        var expectedKeys = Enumerable.Range(0, 14)
            .Select(offset => DateTime.UtcNow.Date.AddDays(-13 + offset).ToString("yyyy-MM-dd"))
            .ToArray();

        stats.RunsPerDay.Keys.Should().BeEquivalentTo(expectedKeys);
    }

    [Fact]
    public async Task Dashboard_RunsPerDay_buckets_runs_into_the_correct_day_and_status()
    {
        var store = _fixture.CreateStore();

        var twoDaysAgo = DateTime.UtcNow.Date.AddDays(-2);
        var fiveDaysAgo = DateTime.UtcNow.Date.AddDays(-5);

        // Two Succeeded + one Failed on day-2, one Cancelled on day-5.
        await store.UpsertRunSummaryAsync(
            MakeSummary(AutomationRunStatus.Succeeded, new DateTimeOffset(twoDaysAgo.AddHours(10), TimeSpan.Zero)),
            "f", "r", CancellationToken.None);
        await store.UpsertRunSummaryAsync(
            MakeSummary(AutomationRunStatus.Succeeded, new DateTimeOffset(twoDaysAgo.AddHours(14), TimeSpan.Zero)),
            "f", "r", CancellationToken.None);
        await store.UpsertRunSummaryAsync(
            MakeSummary(AutomationRunStatus.Failed, new DateTimeOffset(twoDaysAgo.AddHours(16), TimeSpan.Zero)),
            "f", "r", CancellationToken.None);
        await store.UpsertRunSummaryAsync(
            MakeSummary(AutomationRunStatus.Cancelled, new DateTimeOffset(fiveDaysAgo.AddHours(8), TimeSpan.Zero)),
            "f", "r", CancellationToken.None);

        var stats = await _fixture.CreateRunManager().GetDashboardStatsAsync();

        var dayMinus2Key = twoDaysAgo.ToString("yyyy-MM-dd");
        var dayMinus5Key = fiveDaysAgo.ToString("yyyy-MM-dd");

        stats.RunsPerDay[dayMinus2Key].Succeeded.Should().Be(2);
        stats.RunsPerDay[dayMinus2Key].Failed.Should().Be(1);
        stats.RunsPerDay[dayMinus2Key].Cancelled.Should().Be(0);

        stats.RunsPerDay[dayMinus5Key].Succeeded.Should().Be(0);
        stats.RunsPerDay[dayMinus5Key].Failed.Should().Be(0);
        stats.RunsPerDay[dayMinus5Key].Cancelled.Should().Be(1);

        // Every other day must be zeroed out rather than missing.
        foreach (var kvp in stats.RunsPerDay.Where(k => k.Key != dayMinus2Key && k.Key != dayMinus5Key))
        {
            kvp.Value.Succeeded.Should().Be(0);
            kvp.Value.Failed.Should().Be(0);
            kvp.Value.Cancelled.Should().Be(0);
            kvp.Value.Other.Should().Be(0);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────

    private static AutomationRunSummary MakeSummary(AutomationRunStatus status, DateTimeOffset createdAt) => new()
    {
        RunId = Guid.NewGuid(),
        RunName = "DashboardTestRun",
        Scenario = AutomationScenarioKind.Custom,
        SelectedMeasure = string.Empty,
        PatientCount = 10,
        ResourcesPerPatient = 250,
        Seed = 20260329,
        Status = status,
        CreatedAt = createdAt,
        StartedAt = createdAt,
        FinishedAt = createdAt.AddMinutes(1),
        FacilityId = "facility-test",
        ReportId = "report-test",
    };

    private static async Task SeedAsync(
        MongoSnapshotStore store,
        AutomationRunStatus status,
        int days,
        int count)
    {
        for (var i = 0; i < count; i++)
        {
            var run = MakeSummary(status, DateTimeOffset.UtcNow.AddDays(-days).AddMinutes(-i));
            await store.UpsertRunSummaryAsync(run, run.FacilityId, run.ReportId, CancellationToken.None);
        }
    }

    private static async Task SeedCompletedAsync(
        MongoSnapshotStore store,
        AutomationRunStatus status,
        DateTimeOffset createdAt,
        double durationSeconds)
    {
        var run = MakeSummary(status, createdAt);
        run.FinishedAt = createdAt.AddSeconds(durationSeconds);
        await store.UpsertRunSummaryAsync(run, run.FacilityId, run.ReportId, CancellationToken.None);
    }

    private static async Task SeedLegacyAsync(
        MongoDB.Driver.IMongoCollection<MongoDB.Bson.BsonDocument> raw,
        AutomationRunStatus status,
        DateTimeOffset createdAt)
    {
        var finished = createdAt.AddMinutes(1);
        var legacy = new MongoDB.Bson.BsonDocument
        {
            { "_id", Guid.NewGuid().ToString() },
            { "FacilityId", "facility-legacy" },
            { "ReportId", "report-legacy" },
            { "RunName", "LegacyDashboardRun" },
            { "Scenario", AutomationScenarioKind.Custom.ToString() },
            { "SelectedMeasure", string.Empty },
            { "PatientCount", 10 },
            { "ResourcesPerPatient", 250 },
            { "Seed", 20260329 },
            { "RunConfigurationJson", MongoDB.Bson.BsonNull.Value },
            { "Status", status.ToString() },
            { "Error", MongoDB.Bson.BsonNull.Value },
            { "CreatedAt",   new MongoDB.Bson.BsonArray { createdAt.Ticks, (int)createdAt.Offset.TotalMinutes } },
            { "IsActive", false },
            { "StartedAt",   new MongoDB.Bson.BsonArray { createdAt.Ticks, (int)createdAt.Offset.TotalMinutes } },
            { "FinishedAt",  new MongoDB.Bson.BsonArray { finished.Ticks,  (int)finished.Offset.TotalMinutes } },
            { "CompletedAt", new MongoDB.Bson.BsonArray { finished.Ticks,  (int)finished.Offset.TotalMinutes } },
            { "Duration", "1m 0s" },
        };

        await raw.InsertOneAsync(legacy);
    }
}
