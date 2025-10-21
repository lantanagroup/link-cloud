using Automation.UI.Services.Persistence;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.AutomationUI;

/// <summary>
/// Exercises the Mongo-backed dashboard persistence end-to-end against a real
/// MongoDB container. These tests exist because the bugs they guard against
/// only manifest against the real BSON type system and caused the
/// 14-day dashboard chart to silently collapse to "today only" in production.
///
/// Pins that summaries written via <see cref="MongoSnapshotStore"/> are
/// discoverable by the server-side $gte filter used by the dashboard.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class MongoSnapshotStoreDashboardTests : IAsyncLifetime
{
    private readonly AutomationUIIntegrationTestFixture _fixture;

    public MongoSnapshotStoreDashboardTests(AutomationUIIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Upsert_then_GetAll_with_since_filter_returns_recent_runs_and_excludes_old_ones()
    {
        var store = _fixture.CreateStore();

        var recent = MakeSummary(AutomationRunStatus.Succeeded, createdAt: DateTimeOffset.UtcNow.AddDays(-2));
        var ancient = MakeSummary(AutomationRunStatus.Succeeded, createdAt: DateTimeOffset.UtcNow.AddDays(-90));

        await store.UpsertRunSummaryAsync(recent, recent.FacilityId, recent.ReportId, CancellationToken.None);
        await store.UpsertRunSummaryAsync(ancient, ancient.FacilityId, ancient.ReportId, CancellationToken.None);

        var since = DateTimeOffset.UtcNow.AddDays(-14);
        var results = await store.GetAllRunSummariesAsync(since);

        results.Should().ContainSingle(r => r.RunId == recent.RunId);
        results.Should().NotContain(r => r.RunId == ancient.RunId);
    }

    [Fact]
    public async Task Upsert_persists_CreatedAt_as_ISODate_not_as_legacy_array()
    {
        var store = _fixture.CreateStore();
        var summary = MakeSummary(AutomationRunStatus.Succeeded, createdAt: DateTimeOffset.UtcNow.AddHours(-1));

        await store.UpsertRunSummaryAsync(summary, summary.FacilityId, summary.ReportId, CancellationToken.None);

        var raw = await _fixture.RawRunsCollection
            .Find(Builders<BsonDocument>.Filter.Eq("_id", summary.RunId.ToString()))
            .FirstAsync();

        // This is the core contract: CreatedAt must be a BSON Date, not a [ticks, offset]
        // array. Anything else means $gte won't behave correctly on the dashboard query.
        raw["CreatedAt"].BsonType.Should().Be(BsonType.DateTime);
        raw["StartedAt"].BsonType.Should().Be(BsonType.DateTime);
        raw["FinishedAt"].BsonType.Should().Be(BsonType.DateTime);
    }

    [Fact]
    public async Task GetAll_without_since_returns_runs_across_all_dates_sorted_desc()
    {
        var store = _fixture.CreateStore();

        var newer = MakeSummary(AutomationRunStatus.Succeeded, createdAt: DateTimeOffset.UtcNow.AddDays(-1));
        var older = MakeSummary(AutomationRunStatus.Failed, createdAt: DateTimeOffset.UtcNow.AddDays(-100));

        await store.UpsertRunSummaryAsync(newer, newer.FacilityId, newer.ReportId, CancellationToken.None);
        await store.UpsertRunSummaryAsync(older, older.FacilityId, older.ReportId, CancellationToken.None);

        var results = await store.GetAllRunSummariesAsync(since: null);

        results.Select(r => r.RunId).Should().ContainInOrder(newer.RunId, older.RunId);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────

    private static AutomationRunSummary MakeSummary(AutomationRunStatus status, DateTimeOffset createdAt) => new()
    {
        RunId = Guid.NewGuid(),
        RunName = "IntegrationTestRun",
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
}
