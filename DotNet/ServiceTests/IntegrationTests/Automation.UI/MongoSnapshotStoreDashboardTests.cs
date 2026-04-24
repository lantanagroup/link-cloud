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
/// (a) only manifest against the real BSON type system and (b) caused the
/// 14-day dashboard chart to silently collapse to "today only" in production.
///
/// The two scenarios each test class should pin:
///   1. Current code path: summaries written via <see cref="MongoSnapshotStore"/>
///      are discoverable by the server-side $gte filter used by the dashboard.
///   2. Legacy data migration: documents previously written with the driver's
///      default DateTimeOffset-as-array representation are invisible to the
///      $gte filter until <see cref="MongoIndexManager"/>'s startup migration
///      rewrites them, and visible afterwards.
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

    // ─────────────────────────────────────────────────────────────────────
    //  Group 1: current code path
    // ─────────────────────────────────────────────────────────────────────

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
    //  Group 2: legacy-data migration
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Legacy_array_form_document_is_invisible_to_server_side_gte_before_migration()
    {
        var createdAt = DateTimeOffset.UtcNow.AddDays(-3);
        var legacyRunId = Guid.NewGuid();
        await SeedLegacyArrayFormRunAsync(legacyRunId, createdAt);

        var store = _fixture.CreateStore();

        // Sanity: read path is format-tolerant, so a by-id lookup still works.
        var byId = await store.GetRunSummaryAsync(legacyRunId);
        byId.Should().NotBeNull();
        byId!.RunId.Should().Be(legacyRunId);

        // But the dashboard's range query must NOT see the legacy doc, because
        // BSON canonical type order puts every Array below every Date.
        var since = DateTimeOffset.UtcNow.AddDays(-14);
        var results = await store.GetAllRunSummariesAsync(since);

        results.Should().NotContain(r => r.RunId == legacyRunId,
            "legacy array-form dates cannot match a $gte ISODate filter until migrated");
    }

    [Fact]
    public async Task Migration_rewrites_legacy_array_form_dates_to_ISODate_and_restores_gte_visibility()
    {
        var createdAt = DateTimeOffset.UtcNow.AddDays(-3);
        var legacyRunId = Guid.NewGuid();
        await SeedLegacyArrayFormRunAsync(legacyRunId, createdAt);

        // Pre-migration: doc stored as array.
        var preMigration = await _fixture.RawRunsCollection
            .Find(Builders<BsonDocument>.Filter.Eq("_id", legacyRunId.ToString()))
            .FirstAsync();
        preMigration["CreatedAt"].BsonType.Should().Be(BsonType.Array,
            "test seeding should have produced a legacy-shaped document");

        // Run the startup migration (idempotent; also provisions indexes � safe).
        _fixture.CreateIndexManager().EnsureAllIndexes();

        // Post-migration: on-disk shape is now ISODate.
        var postMigration = await _fixture.RawRunsCollection
            .Find(Builders<BsonDocument>.Filter.Eq("_id", legacyRunId.ToString()))
            .FirstAsync();
        postMigration["CreatedAt"].BsonType.Should().Be(BsonType.DateTime);
        postMigration["StartedAt"].BsonType.Should().Be(BsonType.DateTime);
        postMigration["FinishedAt"].BsonType.Should().Be(BsonType.DateTime);
        postMigration["CompletedAt"].BsonType.Should().Be(BsonType.DateTime);

        // And now the dashboard query sees it.
        var store = _fixture.CreateStore();
        var since = DateTimeOffset.UtcNow.AddDays(-14);
        var results = await store.GetAllRunSummariesAsync(since);

        results.Should().ContainSingle(r => r.RunId == legacyRunId);
    }

    [Fact]
    public async Task Migration_preserves_field_values_across_rewrite()
    {
        var createdAt = new DateTimeOffset(2026, 04, 10, 14, 30, 00, TimeSpan.Zero);
        var legacyRunId = Guid.NewGuid();
        await SeedLegacyArrayFormRunAsync(legacyRunId, createdAt);

        _fixture.CreateIndexManager().EnsureAllIndexes();

        var store = _fixture.CreateStore();
        var summary = await store.GetRunSummaryAsync(legacyRunId);

        summary.Should().NotBeNull();
        // Round-trip through array → ISODate loses sub-millisecond precision and the
        // original offset (the driver normalizes to UTC on the DateTime path). Compare
        // at millisecond UTC granularity, which is what Mongo actually stores.
        summary!.CreatedAt.ToUnixTimeMilliseconds()
            .Should().Be(createdAt.ToUnixTimeMilliseconds());
        summary.FacilityId.Should().Be("facility-legacy");
        summary.ReportId.Should().Be("report-legacy");
        summary.Status.Should().Be(AutomationRunStatus.Succeeded);
        summary.PatientCount.Should().Be(10);
        summary.Seed.Should().Be(20260329);
    }

    [Fact]
    public async Task Migration_is_idempotent_and_leaves_already_migrated_documents_untouched()
    {
        var store = _fixture.CreateStore();
        var summary = MakeSummary(AutomationRunStatus.Succeeded, createdAt: DateTimeOffset.UtcNow.AddDays(-1));
        await store.UpsertRunSummaryAsync(summary, summary.FacilityId, summary.ReportId, CancellationToken.None);

        var beforeDoc = await _fixture.RawRunsCollection
            .Find(Builders<BsonDocument>.Filter.Eq("_id", summary.RunId.ToString()))
            .FirstAsync();

        var indexManager = _fixture.CreateIndexManager();
        indexManager.EnsureAllIndexes();
        indexManager.EnsureAllIndexes(); // second call must be a no-op on data

        var afterDoc = await _fixture.RawRunsCollection
            .Find(Builders<BsonDocument>.Filter.Eq("_id", summary.RunId.ToString()))
            .FirstAsync();

        afterDoc["CreatedAt"].BsonType.Should().Be(BsonType.DateTime);
        // Field values should still match byte-for-byte on the fields the migration
        // touches; this catches any accidental normalization drift.
        afterDoc["CreatedAt"].Should().Be(beforeDoc["CreatedAt"]);
        afterDoc["FacilityId"].Should().Be(beforeDoc["FacilityId"]);
        afterDoc["Status"].Should().Be(beforeDoc["Status"]);
    }

    [Fact]
    public async Task Migration_handles_mixed_collection_of_legacy_and_current_documents()
    {
        var store = _fixture.CreateStore();

        // Modern doc (already ISODate)
        var modern = MakeSummary(AutomationRunStatus.Succeeded, createdAt: DateTimeOffset.UtcNow.AddDays(-1));
        await store.UpsertRunSummaryAsync(modern, modern.FacilityId, modern.ReportId, CancellationToken.None);

        // Two legacy docs
        var legacyA = Guid.NewGuid();
        var legacyB = Guid.NewGuid();
        await SeedLegacyArrayFormRunAsync(legacyA, DateTimeOffset.UtcNow.AddDays(-2));
        await SeedLegacyArrayFormRunAsync(legacyB, DateTimeOffset.UtcNow.AddDays(-5));

        _fixture.CreateIndexManager().EnsureAllIndexes();

        var since = DateTimeOffset.UtcNow.AddDays(-14);
        var results = await store.GetAllRunSummariesAsync(since);

        results.Select(r => r.RunId).Should().Contain(new[] { modern.RunId, legacyA, legacyB });
    }

    [Fact]
    public async Task Migration_must_not_mutate_non_date_fields_on_legacy_documents()
    {
        // Regression guard for a subtle bug: if the migration round-trips through the
        // typed class map, the POCO's defaults silently overwrite fields like Status,
        // Scenario, and RunName on the way back out. That caused the dashboard to
        // bucket migrated rows as Queued/"Other" and bump the Active counter
        // incorrectly. The migration must touch ONLY the four date fields.
        var runId = Guid.NewGuid();
        await SeedLegacyArrayFormRunAsync(runId, DateTimeOffset.UtcNow.AddDays(-2));

        // Capture every non-date field value before migration so we can compare byte
        // for byte afterwards.
        var before = await _fixture.RawRunsCollection
            .Find(Builders<BsonDocument>.Filter.Eq("_id", runId.ToString()))
            .FirstAsync();

        var dateFields = new HashSet<string> { "CreatedAt", "StartedAt", "FinishedAt", "CompletedAt" };
        var nonDateFieldsBefore = before.Elements
            .Where(e => !dateFields.Contains(e.Name))
            .ToDictionary(e => e.Name, e => e.Value.DeepClone());

        _fixture.CreateIndexManager().EnsureAllIndexes();

        var after = await _fixture.RawRunsCollection
            .Find(Builders<BsonDocument>.Filter.Eq("_id", runId.ToString()))
            .FirstAsync();

        foreach (var kvp in nonDateFieldsBefore)
        {
            after.Contains(kvp.Key).Should().BeTrue($"field {kvp.Key} must survive migration");
            after[kvp.Key].Should().Be(kvp.Value, $"field {kvp.Key} must not be mutated by migration");
        }

        // And the enum-string status specifically must still parse back into the
        // correct AutomationRunStatus when the store reads it (this is what the
        // dashboard's status-breakdown pie chart binds to).
        var summary = await _fixture.CreateStore().GetRunSummaryAsync(runId);
        summary!.Status.Should().Be(AutomationRunStatus.Succeeded);
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

    /// <summary>
    /// Directly writes a document that mirrors the on-disk shape produced by prior
    /// versions of this service, when DateTimeOffset used the driver's default
    /// Array representation ([ticks, offsetMinutes]). Bypasses the typed class map
    /// on purpose so we can construct a BSON shape the current attribute set would
    /// never emit.
    /// </summary>
    private async Task SeedLegacyArrayFormRunAsync(Guid runId, DateTimeOffset createdAt)
    {
        var started = createdAt;
        var finished = createdAt.AddMinutes(1);

        var legacyDoc = new BsonDocument
        {
            { "_id", runId.ToString() },
            { "FacilityId", "facility-legacy" },
            { "ReportId", "report-legacy" },
            { "RunName", "LegacyRun" },
            { "Scenario", AutomationScenarioKind.Custom.ToString() },
            { "SelectedMeasure", string.Empty },
            { "PatientCount", 10 },
            { "ResourcesPerPatient", 250 },
            { "Seed", 20260329 },
            { "RunConfigurationJson", BsonNull.Value },
            { "Status", AutomationRunStatus.Succeeded.ToString() },
            { "Error", BsonNull.Value },
            { "CreatedAt", ToLegacyArray(createdAt) },
            { "IsActive", false },
            { "StartedAt", ToLegacyArray(started) },
            { "FinishedAt", ToLegacyArray(finished) },
            { "CompletedAt", ToLegacyArray(finished) },
            { "Duration", "1m 0s" },
        };

        await _fixture.RawRunsCollection.InsertOneAsync(legacyDoc);
    }

    /// <summary>Reproduces the MongoDB C# driver's default DateTimeOffset array
    /// representation: <c>[ticks, offsetInMinutes]</c>.</summary>
    private static BsonArray ToLegacyArray(DateTimeOffset value) =>
        new() { value.Ticks, (int)value.Offset.TotalMinutes };
}
