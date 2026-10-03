using LantanaGroup.Link.Automation.Link.Helpers;
using MongoDB.Bson;
using MongoDB.Driver;
using LantanaGroup.Link.Shared.Application.Services.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Automation.UI.Services.Persistence;

/// <summary>
/// MongoDB-backed implementation of <see cref="ISnapshotStore"/>.
/// Uses the existing Mongo instance running in Docker (local) or
/// Cosmos DB for MongoDB API (deployed environments).
///
/// Collections:
///   automation_runs       — lightweight run metadata
///   automation_snapshots  — per-run, per-domain polling data (upsert on RunId+Domain)
///   automation_logs       — full log output per run
///   automation_log_split_claims - lease for a log split that cannot grow its source chunk
///   automation_log_sequence_stamps - sequences for a chunk that cannot store them inline
///
/// Indexes are managed centrally by <see cref="MongoIndexManager"/>.
/// </summary>
public sealed class MongoSnapshotStore : ISnapshotStore
{
    private const int MaxLogLinesPerChunk = 1_000;
    // Escaped JSON size of the lines in one chunk. Cosmos DB for MongoDB RU
    // rejects documents over 2 MB; 1 MB leaves room for field names and sequences.
    internal const int MaxLogChunkEstimatedBsonBytes = 1_048_576;
    // Cosmos DB rejects a request over 2 MB, not only a document. One full-size
    // part fills a bulk command; a second part goes in the next command.
    private const int MaxBulkWriteJsonBytes = 1_048_576;
    internal const int EscapedLogByteCountVersion = 1;
    internal const int EstimatedBsonBytesPerLineOverhead = 64;
    // Split-claim fields and ByteCountVersion. A source this close to the cap
    // is split without writing those fields onto it.
    internal const int LogMetadataReserveBytes = 4096;
    /// <summary>
    /// Test hook invoked after the legacy log document is read and before the
    /// chunk query. Production leaves it null. A split that finishes in that
    /// window must not make GetLogs return the legacy lines twice.
    /// </summary>
    internal static Func<CancellationToken, Task>? AfterLegacyLogReadForTests { get; set; }

    /// <summary>
    /// Test hook that runs after a split claim is stored and before the source
    /// is reloaded. Production leaves it null.
    /// </summary>
    internal static Func<CancellationToken, Task>? AfterLogSplitClaimedForTests { get; set; }

    /// <summary>
    /// Test hook invoked after a split has reloaded its source and before it
    /// inserts replacements. Production leaves it null.
    /// </summary>
    internal static Func<CancellationToken, Task>? BeforeLogReplacementInsertForTests { get; set; }

    /// <summary>
    /// Test hook invoked after parts are written and before the header publish.
    /// Production leaves it null.
    /// </summary>
    internal static Func<CancellationToken, Task>? BeforePartitionHeaderPublishForTests { get; set; }

    /// <summary>
    /// Test hook invoked after a header publish returns and before the commit
    /// check. Production leaves it null. Cleared after one call.
    /// </summary>
    internal static Func<CancellationToken, Task>? AfterPartitionHeaderPublishForTests { get; set; }

    /// <summary>
    /// Test hook invoked after a header id is chosen and before the header
    /// document is written. Production leaves it null.
    /// </summary>
    internal static Func<CancellationToken, Task>? BeforeSnapshotHeaderWriteForTests { get; set; }

    /// <summary>
    /// Test hook invoked after the source chunk is deleted and before its
    /// replacements are published. Production leaves it null.
    /// </summary>
    internal static Func<CancellationToken, Task>? AfterLogSourceDeletedForTests { get; set; }

    /// <summary>
    /// Test hook invoked after a missing source is considered for cleanup.
    /// Production leaves it null.
    /// </summary>
    internal static Func<CancellationToken, Task>? AfterAbandonedSplitConsideredForTests { get; set; }

    /// <summary>
    /// Test hook invoked after an inline header write and before the previous
    /// generation is retired. Production leaves it null.
    /// </summary>
    internal static Func<CancellationToken, Task>? BeforeInlineGenerationRetireForTests { get; set; }

    /// <summary>
    /// Test hook invoked at the start of replacement reconciliation, before
    /// those rows are loaded. Production leaves it null.
    /// </summary>
    internal static Func<CancellationToken, Task>? BeforeReplacementReconcileForTests { get; set; }

    /// <summary>
    /// Test hook invoked after replacements are written and before the source
    /// chunk is deleted. Production leaves it null.
    /// </summary>
    internal static Func<CancellationToken, Task>? BeforeLogSourceDeleteForTests { get; set; }

    /// <summary>
    /// True after one pass found no legacy or oversized log chunks. Later passes
    /// in this process skip the collection scan. A new process scans once.
    /// </summary>
    internal bool LogRepairCaughtUp { get; private set; }
    private const string OversizedLogLineSuffix = " [truncated: exceeded log chunk byte budget]";
    private const string SnapshotPayloadPointerEnvelopeProperty = "__externalSnapshotPayloadPointer";
    private static readonly TimeSpan LogSplitClaimLease = TimeSpan.FromMinutes(5);

    private readonly string _logSplitOwner = Guid.NewGuid().ToString("N");

    private readonly IMongoCollection<AutomationRunDocument> _runs;
    private readonly IMongoCollection<AutomationRunInputDocument> _runInputs;
    private readonly IMongoCollection<DomainSnapshotDocument> _snapshots;
    private readonly IMongoCollection<SnapshotPartDocument> _parts;
    private readonly IMongoCollection<RetiredSnapshotGenerationDocument> _retiredGenerations;
    private readonly IMongoCollection<RunLogDocument> _logs;
    private readonly IMongoCollection<LogSplitClaimDocument> _logSplitClaims;
    private readonly IMongoCollection<LogSequenceStampDocument> _logSequenceStamps;
    private readonly IMongoCollection<RunLogSequenceDocument> _logSequences;
    private readonly IMongoCollection<ImportedBundleDocument> _importedBundles;
    private readonly IMongoCollection<OwnedFacilityTombstoneDocument> _ownedFacilityTombstones;
    private readonly IMongoCollection<FacilityTeardownProgressDocument> _facilityTeardownProgress;
    private readonly ISnapshotPayloadStore _snapshotPayloadStore;
    private readonly ILogger<MongoSnapshotStore> _logger;

    public MongoSnapshotStore(IMongoDatabase database, ILogger<MongoSnapshotStore> logger, ISnapshotPayloadStore? snapshotPayloadStore = null)
    {
        _runs = database.GetCollection<AutomationRunDocument>("automation_runs");
        _runInputs = database.GetCollection<AutomationRunInputDocument>("automation_run_inputs");
        _snapshots = database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        _parts = database.GetCollection<SnapshotPartDocument>(SnapshotPartDocument.CollectionName);
        _retiredGenerations = database.GetCollection<RetiredSnapshotGenerationDocument>(RetiredSnapshotGenerationDocument.CollectionName);
        _logs = database.GetCollection<RunLogDocument>("automation_logs");
        _logSplitClaims = database.GetCollection<LogSplitClaimDocument>(LogSplitClaimDocument.CollectionName);
        _logSequenceStamps = database.GetCollection<LogSequenceStampDocument>(LogSequenceStampDocument.CollectionName);
        _logSequences = database.GetCollection<RunLogSequenceDocument>("automation_log_sequences");
        _importedBundles = database.GetCollection<ImportedBundleDocument>("automation_imported_bundles");
        _ownedFacilityTombstones = database.GetCollection<OwnedFacilityTombstoneDocument>("automation_owned_facility_tombstones");
        _facilityTeardownProgress = database.GetCollection<FacilityTeardownProgressDocument>("automation_facility_teardown_progress");
        _snapshotPayloadStore = snapshotPayloadStore ?? new InlineSnapshotPayloadStore();
        _logger = logger;
    }

    // --- Run metadata ---

    public async Task RegisterRunAsync(Guid runId, RunSnapshotMeta meta, CancellationToken ct = default)
    {
        var update = Builders<AutomationRunDocument>.Update
            .Set(r => r.FacilityId, meta.FacilityId)
            .Set(r => r.ReportId, meta.ReportId)
            .Set(r => r.IsMetricsRun, meta.IsMetricsRun)
            .Set(r => r.IsActive, true)
            .Set(r => r.StartedAt, meta.StartedAt)
            .SetOnInsert(r => r.RunName, $"Run {runId}")
            .SetOnInsert(r => r.Scenario, AutomationScenarioKind.Custom.ToString())
            .SetOnInsert(r => r.Status, AutomationRunStatus.Running.ToString())
            .SetOnInsert(r => r.RunId, runId)
            .SetOnInsert(r => r.CreatedAt, DateTimeOffset.UtcNow);

        await _runs.UpdateOneAsync(r => r.RunId == runId, update, new UpdateOptions { IsUpsert = true }, ct);
    }

    public async Task UpdateRunMetaAsync(Guid runId, string facilityId, string reportId, CancellationToken ct = default)
    {
        var update = Builders<AutomationRunDocument>.Update
            .Set(r => r.FacilityId, facilityId)
            .Set(r => r.ReportId, reportId);

        await _runs.UpdateOneAsync(r => r.RunId == runId, update, cancellationToken: ct);

        // Clear stale domain snapshot data so milestones/entries from a prior report
        // (e.g., initial report before regeneration) don't bleed into the UI.
        await _snapshots.DeleteManyAsync(s => s.RunId == runId, ct);
        await DeletePartsAsync(runId, ct);
        await _retiredGenerations.DeleteManyAsync(t => t.RunId == runId, ct);
        await _snapshotPayloadStore.DeleteRunPayloadsAsync(runId, ct);
    }

    public async Task CompleteRunAsync(Guid runId, string? duration = null, CancellationToken ct = default)
    {
        var update = Builders<AutomationRunDocument>.Update
            .Set(r => r.IsActive, false)
            .Set(r => r.Duration, duration);

        await _runs.UpdateOneAsync(r => r.RunId == runId, update, cancellationToken: ct);
    }

    public async Task UpsertRunSummaryAsync(AutomationRunSummary summary, string? facilityId, string? reportId, CancellationToken ct = default)
    {
        var hasIdentifiers = !string.IsNullOrWhiteSpace(facilityId)
            && !string.IsNullOrWhiteSpace(reportId);

        var updates = new List<UpdateDefinition<AutomationRunDocument>>
        {
            Builders<AutomationRunDocument>.Update.Set(r => r.RunName, summary.RunName),
            Builders<AutomationRunDocument>.Update.Set(r => r.Scenario, summary.Scenario.ToString()),
            Builders<AutomationRunDocument>.Update.Set(r => r.SelectedMeasure, summary.SelectedMeasure),
            Builders<AutomationRunDocument>.Update.Set(r => r.PatientCount, summary.PatientCount),
            Builders<AutomationRunDocument>.Update.Set(r => r.ResourcesPerPatient, summary.ResourcesPerPatient),
            Builders<AutomationRunDocument>.Update.Set(r => r.Seed, summary.Seed),
            Builders<AutomationRunDocument>.Update.Set(r => r.IsMetricsRun, summary.IsMetricsRun),
            Builders<AutomationRunDocument>.Update.Set(r => r.Status, summary.Status.ToString()),
            Builders<AutomationRunDocument>.Update.Set(r => r.CreatedAt, summary.CreatedAt),
            Builders<AutomationRunDocument>.Update.Set(r => r.StartedAt, summary.StartedAt ?? summary.CreatedAt),
            Builders<AutomationRunDocument>.Update.Set(r => r.FinishedAt, summary.FinishedAt),
            Builders<AutomationRunDocument>.Update.Set(r => r.Error, summary.Error),
            Builders<AutomationRunDocument>.Update.Set(r => r.FacilityId, facilityId ?? string.Empty),
            Builders<AutomationRunDocument>.Update.Set(r => r.ReportId, reportId ?? string.Empty),
            Builders<AutomationRunDocument>.Update.Set(r => r.IsActive, hasIdentifiers && summary.Status.IsInProgress() && summary.Status != AutomationRunStatus.CollectingMetrics),
            Builders<AutomationRunDocument>.Update.SetOnInsert(r => r.RunId, summary.RunId)
        };

        // A later summary written before the flag is set must not clear a true marker.
        if (summary.AutomationCreatedFacility)
            updates.Add(Builders<AutomationRunDocument>.Update.Set(r => r.AutomationCreatedFacility, true));

        if (summary.GeneratedTemplateCacheVersionId.HasValue)
            updates.Add(Builders<AutomationRunDocument>.Update.Set(r => r.GeneratedTemplateCacheVersionId, summary.GeneratedTemplateCacheVersionId));
        if (summary.GeneratedTemplateCacheVersionNumber.HasValue)
            updates.Add(Builders<AutomationRunDocument>.Update.Set(r => r.GeneratedTemplateCacheVersionNumber, summary.GeneratedTemplateCacheVersionNumber));
        if (!string.IsNullOrWhiteSpace(summary.GeneratedTemplateCacheScenarioKey))
            updates.Add(Builders<AutomationRunDocument>.Update.Set(r => r.GeneratedTemplateCacheScenarioKey, summary.GeneratedTemplateCacheScenarioKey));
        if (!string.IsNullOrWhiteSpace(summary.GeneratedTemplateSetHash))
            updates.Add(Builders<AutomationRunDocument>.Update.Set(r => r.GeneratedTemplateSetHash, summary.GeneratedTemplateSetHash));

        var update = Builders<AutomationRunDocument>.Update.Combine(updates);

        await _runs.UpdateOneAsync(r => r.RunId == summary.RunId, update, new UpdateOptions { IsUpsert = true }, ct);
    }

    public Task UpsertRunInputAsync(AutomationRunInputSnapshot input, CancellationToken ct = default)
    {
        var update = Builders<AutomationRunInputDocument>.Update
            .Set(d => d.ScenarioId, input.ScenarioId)
            .Set(d => d.ScenarioName, input.ScenarioName)
            .Set(d => d.RunConfigurationJson, input.RunConfigurationJson)
            .Set(d => d.ImportedBundleIds, input.ImportedBundleIds.Distinct().ToList())
            .Set(d => d.UpdatedAt, input.UpdatedAt)
            .SetOnInsert(d => d.RunId, input.RunId)
            .SetOnInsert(d => d.CreatedAt, input.CreatedAt);

        return _runInputs.UpdateOneAsync(d => d.RunId == input.RunId, update, new UpdateOptions { IsUpsert = true }, ct);
    }

    public async Task<IReadOnlyList<RunSnapshotMeta>> GetActiveRunsAsync(CancellationToken ct = default)
    {
        var docs = await _runs.Find(r => r.IsActive).ToListAsync(ct);
        return docs.Select(ToMeta).ToList();
    }

    public async Task<RunSnapshotMeta?> GetRunMetaAsync(Guid runId, CancellationToken ct = default)
    {
        var doc = await _runs.Find(r => r.RunId == runId).FirstOrDefaultAsync(ct);
        return doc == null ? null : ToMeta(doc);
    }

    public async Task<AutomationRunSummary?> GetRunSummaryAsync(Guid runId, CancellationToken ct = default)
    {
        var doc = await _runs.Find(r => r.RunId == runId).FirstOrDefaultAsync(ct);
        if (doc == null)
            return null;

        var summary = ToSummary(doc);
        var input = await GetRunInputAsync(runId, ct);
        if (input != null)
            summary.RunConfigurationJson = await BuildHydratedRunConfigurationJsonAsync(input, ct);

        return summary;
    }

    public async Task<AutomationRunInputSnapshot?> GetRunInputAsync(Guid runId, CancellationToken ct = default)
    {
        var doc = await _runInputs.Find(d => d.RunId == runId).FirstOrDefaultAsync(ct);
        if (doc == null)
            return null;

        return new AutomationRunInputSnapshot
        {
            RunId = doc.RunId,
            ScenarioId = doc.ScenarioId,
            ScenarioName = doc.ScenarioName,
            RunConfigurationJson = doc.RunConfigurationJson,
            ImportedBundleIds = doc.ImportedBundleIds,
            CreatedAt = doc.CreatedAt,
            UpdatedAt = doc.UpdatedAt
        };
    }

    public async Task<IReadOnlyDictionary<Guid, ImportedBundleSnapshot>> GetImportedBundlesByIdsAsync(IEnumerable<Guid> bundleIds, CancellationToken ct = default)
    {
        var ids = bundleIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, ImportedBundleSnapshot>();

        var docs = await _importedBundles
            .Find(Builders<ImportedBundleDocument>.Filter.In(d => d.Id, ids))
            .ToListAsync(ct);

        return docs.ToDictionary(
            d => d.Id,
            d => new ImportedBundleSnapshot
            {
                BundleId = d.Id,
                PatientId = d.PatientId,
                FileName = d.FileName,
                ByteCount = d.ByteCount
            });
    }

    public async Task<PagedRunResult> GetRunsPageAsync(int pageNumber, int pageSize, string? sortBy = null, bool sortDescending = true, CancellationToken ct = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var total = await _runs.CountDocumentsAsync(FilterDefinition<AutomationRunDocument>.Empty, cancellationToken: ct);

        // Build the sort spec from a server-side whitelist. Anything unrecognized
        // (or null) falls back to CreatedAt DESC, the existing default. The client
        // sends short friendly tokens (matched case-insensitively) rather than raw
        // BSON field names so we never bind user input directly into the query.
        var sortBuilder = Builders<AutomationRunDocument>.Sort;
        var primary = (sortBy ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "runname"      => sortDescending ? sortBuilder.Descending(r => r.RunName)      : sortBuilder.Ascending(r => r.RunName),
            "patientcount" => sortDescending ? sortBuilder.Descending(r => r.PatientCount) : sortBuilder.Ascending(r => r.PatientCount),
            "seed"         => sortDescending ? sortBuilder.Descending(r => r.Seed)         : sortBuilder.Ascending(r => r.Seed),
            "status"       => sortDescending ? sortBuilder.Descending(r => r.Status)       : sortBuilder.Ascending(r => r.Status),
            "finishedat"   => sortDescending ? sortBuilder.Descending(r => r.FinishedAt)   : sortBuilder.Ascending(r => r.FinishedAt),
            "createdat"    => sortDescending ? sortBuilder.Descending(r => r.CreatedAt)    : sortBuilder.Ascending(r => r.CreatedAt),
            _              => sortBuilder.Descending(r => r.CreatedAt),
        };

        // Server-side: single-field sort only. Cosmos DB for MongoDB API rejects
        // multi-field ORDER BY queries that don't have a matching composite index
        // ("The order by query does not have a corresponding composite index that
        // it can be served from"), so we cannot append a {RunId: 1} tiebreaker
        // here without provisioning a {SortField, RunId} compound index for every
        // sortable column — see the matching note in MongoIndexManager about the
        // per-write RU cost we are deliberately avoiding. Rows that share the same
        // primary-sort value (e.g. two runs in the same Status bucket) are returned
        // in storage order; in practice this is stable across consecutive page
        // requests because the underlying documents do not move.
        var docs = await _runs.Find(FilterDefinition<AutomationRunDocument>.Empty)
            .Sort(primary)
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(ct);

        var items = docs.Select(ToSummary).ToList();
        return new PagedRunResult(items, pageNumber, pageSize, total);
    }

    public async Task<IReadOnlyList<AutomationRunSummary>> GetAllRunSummariesAsync(DateTimeOffset? since = null, CancellationToken ct = default)
    {
        // CreatedAt is persisted as BSON ISODate (see AutomationRunDocument), so $gte
        // evaluates as a proper date comparison and hits the idx_createdAt_desc index.
        var filter = since.HasValue
            ? Builders<AutomationRunDocument>.Filter.Gte(r => r.CreatedAt, since.Value)
            : FilterDefinition<AutomationRunDocument>.Empty;

        var docs = await _runs.Find(filter)
            .SortByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return docs.Select(ToSummary).ToList();
    }

    public async Task MarkAutomationCreatedFacilityAsync(AutomationRunSummary summary, string facilityId, CancellationToken ct = default)
    {
        var hasIdentifiers = !string.IsNullOrWhiteSpace(facilityId)
            && !string.IsNullOrWhiteSpace(summary.ReportId);
        var isActive = hasIdentifiers
            && summary.Status.IsInProgress()
            && summary.Status != AutomationRunStatus.CollectingMetrics;

        var update = Builders<AutomationRunDocument>.Update
            .Set(r => r.AutomationCreatedFacility, true)
            .Set(r => r.FacilityId, facilityId ?? string.Empty)
            .SetOnInsert(r => r.RunId, summary.RunId)
            .SetOnInsert(r => r.RunName, summary.RunName)
            .SetOnInsert(r => r.Scenario, summary.Scenario.ToString())
            .SetOnInsert(r => r.SelectedMeasure, summary.SelectedMeasure)
            .SetOnInsert(r => r.PatientCount, summary.PatientCount)
            .SetOnInsert(r => r.ResourcesPerPatient, summary.ResourcesPerPatient)
            .SetOnInsert(r => r.Seed, summary.Seed)
            .SetOnInsert(r => r.IsMetricsRun, summary.IsMetricsRun)
            .SetOnInsert(r => r.Status, summary.Status.ToString())
            .SetOnInsert(r => r.CreatedAt, summary.CreatedAt)
            .SetOnInsert(r => r.StartedAt, summary.StartedAt ?? summary.CreatedAt)
            .SetOnInsert(r => r.FinishedAt, summary.FinishedAt)
            .SetOnInsert(r => r.Error, summary.Error)
            .SetOnInsert(r => r.ReportId, summary.ReportId ?? string.Empty)
            .SetOnInsert(r => r.IsActive, isActive);

        await _runs.UpdateOneAsync(
            r => r.RunId == summary.RunId,
            update,
            new UpdateOptions { IsUpsert = true },
            ct);
    }

    public async Task RetainOwnedFacilitiesAsync(AutomationRunSummary summary, CancellationToken ct = default)
    {
        foreach (var facilityId in DistinctOwnedFacilityIds(summary))
        {
            await _ownedFacilityTombstones.ReplaceOneAsync(
                t => t.FacilityId == facilityId,
                new OwnedFacilityTombstoneDocument
                {
                    FacilityId = facilityId,
                    RunId = summary.RunId.ToString(),
                    CreatedAt = DateTimeOffset.UtcNow,
                    EligibleAt = RunCleanupHelper.RunTimestamp(summary)
                },
                new ReplaceOptions { IsUpsert = true },
                ct);
        }
    }

    internal static IReadOnlyList<string> DistinctOwnedFacilityIds(AutomationRunSummary summary)
    {
        var ids = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var facilityId in new[] { summary.FacilityId, summary.RunId.ToString() })
        {
            if (!RunCleanupHelper.IsOwnedAutomationFacilityId(summary, facilityId) || string.IsNullOrWhiteSpace(facilityId))
                continue;
            if (seen.Add(facilityId))
                ids.Add(facilityId);
        }

        return ids;
    }

    public async Task<IReadOnlyList<RetainedFacility>> GetRetainedFacilitiesAsync(CancellationToken ct = default)
    {
        var docs = await _ownedFacilityTombstones.Find(FilterDefinition<OwnedFacilityTombstoneDocument>.Empty)
            .ToListAsync(ct);
        return docs.Select(doc => new RetainedFacility(doc.FacilityId, doc.EligibleAt)).ToList();
    }

    public async Task ReleaseRetainedFacilityAsync(string facilityId, CancellationToken ct = default)
        => await _ownedFacilityTombstones.DeleteOneAsync(t => t.FacilityId == facilityId, ct);

    public async Task MarkFacilityTeardownProgressAsync(Guid runId, string facilityId, CancellationToken ct = default)
    {
        var exists = await _facilityTeardownProgress.Find(p => p.RunId == runId && p.FacilityId == facilityId)
            .AnyAsync(ct);
        if (!exists)
        {
            await _facilityTeardownProgress.InsertOneAsync(new FacilityTeardownProgressDocument
            {
                Id = ObjectId.GenerateNewId(),
                RunId = runId,
                FacilityId = facilityId
            }, cancellationToken: ct);
        }
    }

    public async Task<IReadOnlyList<string>> GetFacilityTeardownProgressAsync(Guid runId, CancellationToken ct = default)
    {
        var docs = await _facilityTeardownProgress.Find(p => p.RunId == runId).ToListAsync(ct);
        return docs.Select(doc => doc.FacilityId).ToList();
    }

    public async Task ClearFacilityTeardownProgressAsync(Guid runId, CancellationToken ct = default)
        => await _facilityTeardownProgress.DeleteManyAsync(p => p.RunId == runId, ct);

    public Task DeleteRunAsync(Guid runId, CancellationToken ct = default)
        => DeleteRunAsync(runId, retainOwnedFacilities: true, ct);

    public async Task DeleteRunAsync(Guid runId, bool retainOwnedFacilities, CancellationToken ct = default)
    {
        var run = await _runs.Find(r => r.RunId == runId).FirstOrDefaultAsync(ct);
        if (run != null && retainOwnedFacilities)
            await RetainOwnedFacilitiesAsync(ToSummary(run), ct);

        // Drop child history first and the run summary last. A failure after the
        // summary is gone would leave history that the next purge can no longer select.
        await _runInputs.DeleteOneAsync(r => r.RunId == runId, ct);
        await _snapshots.DeleteManyAsync(s => s.RunId == runId, ct);
        await DeletePartsAsync(runId, ct);
        await _retiredGenerations.DeleteManyAsync(t => t.RunId == runId, ct);
        await _logs.DeleteManyAsync(CreateLogChunkFilter(runId), ct);
        await _logs.DeleteOneAsync(l => l.Id == runId.ToString(), ct);
        await _logSequenceStamps.DeleteManyAsync(CreateSequenceStampFilter(runId), ct);
        await _logSplitClaims.DeleteManyAsync(CreateLogSplitClaimFilter(runId), ct);

        // Payload blobs follow the Mongo child rows so a DB failure cannot orphan
        // pointer records that still reference payload data. The summary stays until
        // those deletes succeed.
        await _snapshotPayloadStore.DeleteRunPayloadsAsync(runId, ct);
        await ClearFacilityTeardownProgressAsync(runId, ct);
        await _runs.DeleteOneAsync(r => r.RunId == runId, ct);
    }

    private async Task<string?> BuildHydratedRunConfigurationJsonAsync(AutomationRunInputSnapshot input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.RunConfigurationJson))
            return null;

        try
        {
            var root = JsonNode.Parse(input.RunConfigurationJson) as JsonObject;
            if (root == null)
                return input.RunConfigurationJson;

            if (input.ImportedBundleIds.Count == 0)
                return root.ToJsonString();

            var bundles = await GetImportedBundlesByIdsAsync(input.ImportedBundleIds, ct);
            var byPatient = bundles.Values
                .GroupBy(b => b.PatientId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => new Queue<ImportedBundleSnapshot>(g));

            if (root["importedPatientBundles"] is JsonArray arr)
            {
                foreach (var node in arr.OfType<JsonObject>())
                {
                    var uploadedId = node["uploadedBundleId"]?.GetValue<string>();
                    ImportedBundleSnapshot? bundle = null;

                    if (Guid.TryParse(uploadedId, out var bundleId) && bundles.TryGetValue(bundleId, out var byId))
                    {
                        bundle = byId;
                    }
                    else
                    {
                        var patientId = node["patientId"]?.GetValue<string>() ?? string.Empty;
                        if (byPatient.TryGetValue(patientId, out var queue) && queue.Count > 0)
                            bundle = queue.Dequeue();
                    }

                    if (bundle != null)
                    {
                        node["uploadedBundleId"] = bundle.BundleId.ToString();
                        node["patientId"] = string.IsNullOrWhiteSpace(node["patientId"]?.GetValue<string>()) ? bundle.PatientId : node["patientId"]?.GetValue<string>();
                        node["fileName"] = string.IsNullOrWhiteSpace(node["fileName"]?.GetValue<string>()) ? bundle.FileName : node["fileName"]?.GetValue<string>();
                        node["bundleJson"] = null;
                    }
                }
            }

            return root.ToJsonString();
        }
        catch
        {
            return input.RunConfigurationJson;
        }
    }

    private static RunSnapshotMeta ToMeta(AutomationRunDocument doc) => new()
    {
        RunId = doc.RunId,
        FacilityId = doc.FacilityId,
        ReportId = doc.ReportId,
        StartedAt = doc.StartedAt,
        IsActive = doc.IsActive,
        IsMetricsRun = doc.IsMetricsRun
    };

    private static AutomationRunSummary ToSummary(AutomationRunDocument doc)
    {
        var scenarioParsed = Enum.TryParse<AutomationScenarioKind>(doc.Scenario, ignoreCase: true, out var scenario);
        var statusParsed = Enum.TryParse<AutomationRunStatus>(doc.Status, ignoreCase: true, out var status);

        if (!scenarioParsed)
            scenario = AutomationScenarioKind.Custom;

        if (!statusParsed)
            status = AutomationRunStatus.Failed;

        return new AutomationRunSummary
        {
            RunId = doc.RunId,
            RunName = string.IsNullOrWhiteSpace(doc.RunName)
                ? (scenario == AutomationScenarioKind.Custom ? $"Run {doc.RunId}" : scenario.ToString())
                : doc.RunName,
            Scenario = scenario,
            SelectedMeasure = doc.SelectedMeasure,
            PatientCount = doc.PatientCount,
            ResourcesPerPatient = doc.ResourcesPerPatient,
            Seed = doc.Seed,
            IsMetricsRun = doc.IsMetricsRun,
            RunConfigurationJson = null,
            Status = status,
            CreatedAt = doc.CreatedAt,
            StartedAt = doc.StartedAt,
            FinishedAt = doc.FinishedAt,
            Error = doc.Error,
            Duration = doc.Duration,
            FacilityId = doc.FacilityId,
            AutomationCreatedFacility = doc.AutomationCreatedFacility,
            ReportId = doc.ReportId,
            GeneratedTemplateCacheVersionId = doc.GeneratedTemplateCacheVersionId,
            GeneratedTemplateCacheVersionNumber = doc.GeneratedTemplateCacheVersionNumber,
            GeneratedTemplateCacheScenarioKey = doc.GeneratedTemplateCacheScenarioKey,
            GeneratedTemplateSetHash = doc.GeneratedTemplateSetHash,
            Logs = []
        };
    }

    // --- Domain snapshots ---

    public async Task SetDomainAsync<T>(Guid runId, string domain, T data, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(data);
        var plan = SnapshotPartitioner.Plan(json);
        var target = await ResolveHeaderWriteAsync(runId, domain, ct);
        var existingPointer = TryReadSnapshotPayloadPointer(target.Existing?.Data);

        if (plan is SnapshotPlan.Inline inline)
        {
            SnapshotPartitioner.EnsureWithinHardCap(inline.Json);
            var before = await ReplaceHeaderAsync(target.Filter, target.HeaderId, runId, domain, inline.Json, target.Upsert, ct);
            await DeleteReplacedGenerationAsync(target.HeaderId, before, ct);
            await DeleteStaleHeadersAsync(runId, domain, target.HeaderId, ct);
        }
        else if (plan is SnapshotPlan.Partitioned partitioned)
        {
            await CommitPartitionAsync(target.HeaderId, target.Upsert, runId, domain, partitioned, ct);
            await DeleteStaleHeadersAsync(runId, domain, target.HeaderId, ct);
        }

        if (existingPointer != null)
            await _snapshotPayloadStore.DeleteIfExistsAsync(existingPointer, ct);
    }

    public async Task<DomainSnapshot<T>?> GetDomainAsync<T>(Guid runId, string domain, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var doc = await ReadHeaderAsync(runId, domain, ct);
            if (doc == null)
            {
                _logger.LogDebug("[Store] GetDomain: no document for run={RunId} domain={Domain}", runId.ToString().SanitizeForLog(), domain.SanitizeForLog());
                return null;
            }

            try
            {
                if (SnapshotPartitionHeader.TryRead(doc.Data, out var header) && header != null)
                {
                    var pieces = await LoadCommittedPiecesAsync(runId, domain, header, ct);
                    var payloadJson = SnapshotPartitioner.ReadCommitted(doc.Data, pieces);
                    if (payloadJson == null)
                    {
                        if (attempt < 2)
                        {
                            await Task.Delay(50, ct);
                            continue;
                        }

                        var sanitizedRunId = runId.ToString().SanitizeForLog();
                        var sanitizedDomain = domain.SanitizeForLog();
                        _logger.LogWarning(
                            "[Store] GetDomain: partitioned snapshot for run={RunId} domain={Domain} was incomplete",
                            sanitizedRunId,
                            sanitizedDomain);
                        return null;
                    }

                    return DeserializeDomain<T>(runId, domain, doc, payloadJson);
                }

                var inlineJson = doc.Data;
                var pointer = TryReadSnapshotPayloadPointer(inlineJson);
                if (pointer != null)
                {
                    inlineJson = await _snapshotPayloadStore.ReadAsync(pointer, ct);
                    if (string.IsNullOrWhiteSpace(inlineJson))
                    {
                        var sanitizedRunId = runId.ToString().SanitizeForLog();
                        var sanitizedDomain = domain.SanitizeForLog();
                        var sanitizedBlobName = pointer.BlobName.SanitizeForLog();
                        _logger.LogWarning("[Store] GetDomain: externalized payload missing for run={RunId} domain={Domain} blob={Blob}", sanitizedRunId, sanitizedDomain, sanitizedBlobName);
                        return null;
                    }
                }

                return DeserializeDomain<T>(runId, domain, doc, inlineJson);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "[Store] GetDomain: deserialization failed for run={RunId} domain={Domain} type={Type} (json length={Len})", runId.ToString().SanitizeForLog(), domain.SanitizeForLog(), typeof(T).Name, doc.Data?.Length ?? 0);
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Rewrites one legacy snapshot document when its payload has not changed
    /// since it was read. A concurrent poll that stored a newer payload wins.
    /// </summary>
    public async Task<bool> TryUpgradeLegacyDomainAsync(DomainSnapshotDocument legacy, CancellationToken ct = default)
    {
        if (legacy == null || string.IsNullOrWhiteSpace(legacy.Data))
            return false;

        if (!SnapshotPartitioner.ShouldTranslateStoredData(legacy.Data))
            return false;

        var chosen = await ChooseHeaderAsync(legacy.RunId, legacy.Domain, ct);
        if (chosen == null || chosen.Id != legacy.Id)
        {
            // An older writer does not store a revision, so a missing revision
            // plus an equal timestamp is not proof that this header is stale.
            var revisionless = string.IsNullOrEmpty(legacy.Revision);
            var older = chosen != null && (revisionless
                ? legacy.UpdatedAt < chosen.UpdatedAt
                : legacy.UpdatedAt <= chosen.UpdatedAt);
            if (older)
            {
                await CosmosThrottle.ExecuteAsync(
                    token => _snapshots.DeleteOneAsync(ObservedSnapshot(legacy), token),
                    ct,
                    _logger);
            }

            return false;
        }

        var pointer = TryReadSnapshotPayloadPointer(legacy.Data);
        var payloadJson = legacy.Data;
        if (pointer != null)
        {
            payloadJson = await _snapshotPayloadStore.ReadAsync(pointer, ct);
            if (string.IsNullOrWhiteSpace(payloadJson))
                return false;
        }

        var plan = SnapshotPartitioner.Plan(payloadJson);
        // Match the id and the revision. The legacy payload can already be near
        // 2 MB, and putting it in the update filter makes the command exceed the
        // cap. A document with no revision also has to carry the timestamp that
        // was read, because an older writer changes the payload without a revision.
        var filter = ObservedSnapshot(legacy);

        if (plan is SnapshotPlan.Inline inline)
        {
            SnapshotPartitioner.EnsureWithinHardCap(inline.Json);
            var before = await ReplaceHeaderAsync(
                filter, legacy.Id, legacy.RunId, legacy.Domain, inline.Json, upsert: false, ct, legacy.UpdatedAt);
            if (before == null)
                return false;

            await DeleteReplacedGenerationAsync(legacy.Id, before, ct);
            await DeleteStaleHeadersAsync(legacy.RunId, legacy.Domain, legacy.Id, ct);
            if (pointer != null)
                await _snapshotPayloadStore.DeleteIfExistsAsync(pointer, ct);
            return true;
        }

        if (plan is not SnapshotPlan.Partitioned partitioned)
            return false;

        var committed = await CommitPartitionAsync(
            legacy.Id, upsert: false, legacy.RunId, legacy.Domain, partitioned, ct, filter, legacy.UpdatedAt);
        if (!committed)
            return false;

        await DeleteStaleHeadersAsync(legacy.RunId, legacy.Domain, legacy.Id, ct);

        if (pointer != null)
            await _snapshotPayloadStore.DeleteIfExistsAsync(pointer, ct);
        return true;
    }

    public async Task<int> SplitOversizedLogChunksAsync(CancellationToken ct = default)
    {
        if (LogRepairCaughtUp)
            return 0;

        var sawLegacy = await RewriteLegacyLogByteCountsAsync(ct);
        var split = 0;
        var oversized = await _logs.Find(l => l.BsonByteCount > MaxLogChunkEstimatedBsonBytes
                || l.LineCount > MaxLogLinesPerChunk
                || l.ByteCountVersion != EscapedLogByteCountVersion)
            .SortBy(l => l.Id)
            .Limit(25)
            .ToListAsync(ct);
        if (!sawLegacy && oversized.Count == 0)
        {
            LogRepairCaughtUp = true;
            return 0;
        }

        foreach (var candidate in oversized)
        {
            ct.ThrowIfCancellationRequested();
            // An earlier chunk in this batch may have stamped sequences onto this one.
            var chunk = await _logs.Find(l => l.Id == candidate.Id).FirstOrDefaultAsync(ct);
            if (chunk == null || chunk.Lines.Count == 0)
                continue;

            var metadataFits = LogMetadataFits(chunk);
            await OverlaySequenceStampsAsync([chunk], ct);
            var firstSequence = await FallbackSequenceBeforeChunkAsync(chunk, ct);
            await StampFollowingUnsequencedChunksAsync(chunk, firstSequence, ct);
            var planned = PlanLogSplit(chunk, firstSequence);
            if (planned.Count == 0)
                continue;

            var start = await ClaimLogSplitStartAsync(chunk, planned.Count, metadataFits, ct);
            if (start == null)
                continue;

            if (chunk.ResumePublishing)
            {
                if (await FinishExternalPublishAsync(chunk, start.Value, chunk.SplitCount ?? planned.Count, ct))
                {
                    chunk.ResumePublishing = false;
                    split++;
                    continue;
                }

                chunk.ResumePublishing = false;
                var replanned = await ReplanChangedExternalSplitAsync(chunk, metadataFits, ct);
                if (replanned == null || chunk.SplitStart == null)
                    continue;

                planned = replanned;
                start = chunk.SplitStart;
            }

            var fresh = await _logs.Find(l => l.Id == chunk.Id).FirstOrDefaultAsync(ct);
            if (fresh == null)
            {
                await DeleteAbandonedSplitAsync(chunk, start.Value, planned.Count, ct);
                continue;
            }

            await OverlaySequenceStampsAsync([fresh], ct);
            if (fresh.LineCount != chunk.LineCount
                || !fresh.Lines.SequenceEqual(chunk.Lines)
                || !SameSequences(fresh.LineSequences, chunk.LineSequences))
            {
                chunk.Lines = fresh.Lines;
                chunk.LineCount = fresh.LineCount;
                chunk.LineSequences = fresh.LineSequences;
                chunk.BsonByteCount = fresh.BsonByteCount;
                chunk.ByteCountVersion = fresh.ByteCountVersion;
                if (!await StillOwnLogSplitAsync(chunk.Id, metadataFits, ct))
                    continue;

                await ReconcileAbandonedReplacementsAsync(chunk, predecessorsOnly: false, ct);
                firstSequence = await FallbackSequenceBeforeChunkAsync(chunk, ct);
                await StampFollowingUnsequencedChunksAsync(chunk, firstSequence, ct);
                planned = PlanLogSplit(chunk, firstSequence);
                if (planned.Count == 0)
                    continue;

                chunk.SplitStart = null;
                start = await ClaimLogSplitStartAsync(chunk, planned.Count, metadataFits, ct, resumeSavedStart: false);
                if (start == null)
                    continue;
            }

            var write = await WriteSplitReplacementsAsync(chunk, planned, start.Value, metadataFits, ct);
            start = write.Start;
            if (start == null || !await StillOwnLogSplitAsync(chunk.Id, metadataFits, ct))
            {
                if (start != null && !await _logs.Find(l => l.Id == chunk.Id).AnyAsync(ct))
                    await DeleteAbandonedSplitAsync(chunk, start.Value, planned.Count, ct);
                continue;
            }

            if (!write.Wrote)
            {
                throw new InvalidOperationException(
                    $"Could not split log chunk '{chunk.Id.SanitizeForLog()}' without overwriting another chunk.");
            }

            // The source cannot store the attempt. Record it on the claim before
            // the delete, so a takeover finishes these replacements instead of
            // removing them and then finding the source already gone.
            if (!metadataFits)
            {
                if (!await PublishExternalSplitAsync(chunk, planned, start.Value, metadataFits, ct))
                    continue;

                split++;
                continue;
            }

            await ReconcileAbandonedReplacementsAsync(chunk, predecessorsOnly: true, ct);
            if (!await SplitAttemptStillOwnedAsync(chunk, ct))
            {
                if (!await _logs.Find(l => l.Id == chunk.Id).AnyAsync(ct))
                    await DeleteAbandonedSplitAsync(chunk, start.Value, planned.Count, ct);
                continue;
            }

            if (BeforeLogSourceDeleteForTests != null)
            {
                var beforeSourceDelete = BeforeLogSourceDeleteForTests;
                BeforeLogSourceDeleteForTests = null;
                await beforeSourceDelete(ct);
            }

            var deleteFilter = Builders<RunLogDocument>.Filter.Eq(l => l.Id, chunk.Id)
                & Builders<RunLogDocument>.Filter.Eq(l => l.LineCount, chunk.LineCount);
            if (metadataFits)
                deleteFilter &= Builders<RunLogDocument>.Filter.Eq(l => l.SplitOwner, _logSplitOwner);
            var deleted = await CosmosThrottle.ExecuteAsync(
                token => _logs.DeleteOneAsync(deleteFilter, token),
                ct,
                _logger);
            if (deleted.DeletedCount != 1)
            {
                if (!await _logs.Find(l => l.Id == chunk.Id).AnyAsync(ct))
                    await DeleteAbandonedSplitAsync(chunk, start.Value, planned.Count, ct);
                continue;
            }

            await CosmosThrottle.ExecuteAsync(
                token => _logSequenceStamps.DeleteOneAsync(s => s.Id == chunk.Id, token),
                ct,
                _logger);

            if (!metadataFits)
            {
                await CosmosThrottle.ExecuteAsync(
                    token => _logSplitClaims.DeleteOneAsync(
                        c => c.Id == chunk.Id && c.Owner == _logSplitOwner,
                        token),
                    ct,
                    _logger);
            }

            if (AfterLogSourceDeletedForTests != null)
            {
                var sourceDeleted = AfterLogSourceDeletedForTests;
                AfterLogSourceDeletedForTests = null;
                await sourceDeleted(ct);
            }

            await CosmosThrottle.ExecuteAsync(
                token => _logs.UpdateManyAsync(
                    l => l.SplitFromId == chunk.Id
                        && l.SplitAttempt == chunk.SplitAttempt
                        && l.ChunkNumber >= start.Value
                        && l.ChunkNumber < start.Value + planned.Count,
                    Builders<RunLogDocument>.Update.Unset(l => l.SplitFromId),
                    cancellationToken: token),
                ct,
                _logger);
            split++;
        }

        return split;
    }

    private List<RunLogDocument> PlanLogSplit(RunLogDocument chunk, long fallbackSequence)
    {
        var sequences = chunk.LineSequences ?? [];
        var planned = new List<RunLogDocument>();
        var bufferLines = new List<string>();
        var bufferSequences = new List<long>();
        var bufferBytes = 0;

        void Flush()
        {
            if (bufferLines.Count == 0)
                return;

            planned.Add(new RunLogDocument
            {
                RunId = chunk.RunId,
                LineCount = bufferLines.Count,
                BsonByteCount = bufferBytes,
                ByteCountVersion = EscapedLogByteCountVersion,
                Lines = [.. bufferLines],
                LineSequences = [.. bufferSequences]
            });
            bufferLines.Clear();
            bufferSequences.Clear();
            bufferBytes = 0;
        }

        for (var i = 0; i < chunk.Lines.Count; i++)
        {
            var line = NormalizeLineForChunkBudget(chunk.RunId, chunk.Lines[i]);
            var lineBytes = EstimateLogLineBsonBytes(line);
            var sequence = NextLogSequence(i, sequences, ref fallbackSequence);
            if (bufferLines.Count > 0
                && (bufferLines.Count >= MaxLogLinesPerChunk
                    || bufferBytes + lineBytes > MaxLogChunkEstimatedBsonBytes))
            {
                Flush();
            }

            bufferLines.Add(line);
            bufferSequences.Add(sequence);
            bufferBytes += lineBytes;
        }

        Flush();
        return planned;
    }

    private async Task<int?> ClaimLogSplitStartAsync(
        RunLogDocument chunk,
        int replacementCount,
        bool metadataFits,
        CancellationToken ct,
        bool resumeSavedStart = true)
    {
        if (!metadataFits && resumeSavedStart)
            await CopyExternalClaimOntoChunkAsync(chunk, ct);

        int start;
        // A new claim always gets a new attempt id. Replacements from the saved
        // attempt must be reconciled even when this claim reuses that range.
        var reconcilePredecessors = chunk.SplitStart != null;
        if (chunk.SplitStart is int existing
            && !await LogSplitRangeCollidesAsync(chunk, existing, replacementCount, ct))
        {
            start = existing;
        }
        else
        {
            // Chunk ids are zero-padded, so _id order matches chunk order and
            // the range query can use the _id index. Cosmos rejects a sort on
            // ChunkNumber because that field is not indexed.
            var maxChunkNumber = await _logs.Find(CreateLogChunkFilter(chunk.RunId))
                .SortByDescending(l => l.Id)
                .Project(l => l.ChunkNumber)
                .FirstOrDefaultAsync(ct);
            var candidate = maxChunkNumber + 1;
            if (chunk.SplitStart is int prior)
                candidate = Math.Max(candidate, prior + Math.Max(replacementCount, 1));

            while (await LogSplitRangeCollidesAsync(chunk, candidate, replacementCount, ct))
                candidate += Math.Max(replacementCount, 1);

            start = candidate;
        }

        var claimed = metadataFits
            ? await TryClaimLogSplitAsync(chunk, start, replacementCount, ct)
            : await TryClaimExternalLogSplitAsync(chunk, start, replacementCount, ct);
        if (!claimed)
            return null;

        if (chunk.ResumePublishing)
        {
            if (chunk.SplitStart == null)
                return null;

            if (AfterLogSplitClaimedForTests != null)
                await AfterLogSplitClaimedForTests(ct);

            if (!await SplitAttemptStillOwnedAsync(chunk, ct))
                return null;

            return chunk.SplitStart;
        }

        if (reconcilePredecessors)
            await ReconcileAbandonedReplacementsAsync(chunk, predecessorsOnly: true, ct);

        if (!await SplitAttemptStillOwnedAsync(chunk, ct))
            return null;

        if (AfterLogSplitClaimedForTests != null)
            await AfterLogSplitClaimedForTests(ct);

        return start;
    }

    private async Task CopyExternalClaimOntoChunkAsync(RunLogDocument chunk, CancellationToken ct)
    {
        var claim = await _logSplitClaims.Find(c => c.Id == chunk.Id).FirstOrDefaultAsync(ct);
        if (claim == null)
            return;

        chunk.SplitStart = claim.SplitStart;
        chunk.SplitCount = claim.SplitCount;
        chunk.SplitOwner = claim.Owner;
        chunk.SplitClaimedAt = claim.ClaimedAt;
        chunk.SplitAttempt = claim.AttemptId;
    }

    private async Task<bool> TryClaimExternalLogSplitAsync(RunLogDocument chunk, int start, int count, CancellationToken ct)
    {
        if (await TryResumePublishingClaimAsync(chunk, ct))
            return true;

        var now = DateTimeOffset.UtcNow;
        var cutoff = now - LogSplitClaimLease;
        var filter = Builders<LogSplitClaimDocument>.Filter.Eq(c => c.Id, chunk.Id)
            & Builders<LogSplitClaimDocument>.Filter.Or(
                Builders<LogSplitClaimDocument>.Filter.Exists(c => c.PublishingAttempt, false),
                Builders<LogSplitClaimDocument>.Filter.Eq(c => c.PublishingAttempt, null),
                Builders<LogSplitClaimDocument>.Filter.Eq(c => c.PublishingAttempt, string.Empty))
            & Builders<LogSplitClaimDocument>.Filter.Or(
                Builders<LogSplitClaimDocument>.Filter.Exists(c => c.Owner, false),
                Builders<LogSplitClaimDocument>.Filter.Eq(c => c.Owner, null),
                Builders<LogSplitClaimDocument>.Filter.Eq(c => c.Owner, string.Empty),
                Builders<LogSplitClaimDocument>.Filter.Eq(c => c.Owner, _logSplitOwner),
                Builders<LogSplitClaimDocument>.Filter.Lt(c => c.ClaimedAt, cutoff));
        var attempt = Guid.NewGuid().ToString("N");
        var update = Builders<LogSplitClaimDocument>.Update
            .Set(c => c.Owner, _logSplitOwner)
            .Set(c => c.ClaimedAt, now)
            .Set(c => c.SplitStart, start)
            .Set(c => c.SplitCount, count)
            .Set(c => c.AttemptId, attempt)
            .SetOnInsert(c => c.Id, chunk.Id);
        LogSplitClaimDocument? claimed;
        try
        {
            claimed = await CosmosThrottle.ExecuteAsync(
                token => _logSplitClaims.FindOneAndUpdateAsync(
                    filter,
                    update,
                    new FindOneAndUpdateOptions<LogSplitClaimDocument>
                    {
                        IsUpsert = true,
                        ReturnDocument = ReturnDocument.After
                    },
                    token),
                ct,
                _logger);
        }
        catch (Exception ex) when (IsDuplicateKey(ex))
        {
            return false;
        }

        if (claimed == null || !string.Equals(claimed.Owner, _logSplitOwner, StringComparison.Ordinal))
            return false;

        chunk.SplitOwner = _logSplitOwner;
        chunk.SplitStart = start;
        chunk.SplitCount = count;
        chunk.SplitClaimedAt = now;
        chunk.SplitAttempt = attempt;
        return true;
    }

    /// <summary>
    /// Adopts a claim whose replacements are already durable. The attempt id
    /// stays the same, so the caller publishes those rows instead of deleting them.
    /// </summary>
    private async Task<bool> TryResumePublishingClaimAsync(RunLogDocument chunk, CancellationToken ct)
    {
        var existing = await _logSplitClaims.Find(c => c.Id == chunk.Id).FirstOrDefaultAsync(ct);
        if (existing == null
            || string.IsNullOrEmpty(existing.PublishingAttempt)
            || !string.Equals(existing.AttemptId, existing.PublishingAttempt, StringComparison.Ordinal))
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        var cutoff = now - LogSplitClaimLease;
        if (!string.Equals(existing.Owner, _logSplitOwner, StringComparison.Ordinal)
            && existing.ClaimedAt >= cutoff)
        {
            return false;
        }

        var filter = Builders<LogSplitClaimDocument>.Filter.Eq(c => c.Id, chunk.Id)
            & Builders<LogSplitClaimDocument>.Filter.Eq(c => c.AttemptId, existing.AttemptId)
            & Builders<LogSplitClaimDocument>.Filter.Eq(c => c.PublishingAttempt, existing.PublishingAttempt)
            & Builders<LogSplitClaimDocument>.Filter.Or(
                Builders<LogSplitClaimDocument>.Filter.Eq(c => c.Owner, _logSplitOwner),
                Builders<LogSplitClaimDocument>.Filter.Lt(c => c.ClaimedAt, cutoff));
        var claimed = await CosmosThrottle.ExecuteAsync(
            token => _logSplitClaims.FindOneAndUpdateAsync(
                filter,
                Builders<LogSplitClaimDocument>.Update
                    .Set(c => c.Owner, _logSplitOwner)
                    .Set(c => c.ClaimedAt, now),
                new FindOneAndUpdateOptions<LogSplitClaimDocument> { ReturnDocument = ReturnDocument.After },
                token),
            ct,
            _logger);
        if (claimed == null
            || !string.Equals(claimed.Owner, _logSplitOwner, StringComparison.Ordinal)
            || !string.Equals(claimed.AttemptId, existing.PublishingAttempt, StringComparison.Ordinal))
        {
            return false;
        }

        chunk.SplitOwner = _logSplitOwner;
        chunk.SplitStart = claimed.SplitStart;
        chunk.SplitCount = claimed.SplitCount;
        chunk.SplitClaimedAt = now;
        chunk.SplitAttempt = claimed.AttemptId;
        chunk.ResumePublishing = true;
        return true;
    }

    /// <summary>
    /// Records that this attempt's replacements are the copy that must survive
    /// if another worker takes the lease before the source delete lands.
    /// </summary>
    private async Task<bool> MarkExternalSplitPublishingAsync(RunLogDocument chunk, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(chunk.SplitAttempt))
            return false;

        var now = DateTimeOffset.UtcNow;
        var cutoff = now - LogSplitClaimLease;
        var updated = await CosmosThrottle.ExecuteAsync(
            token => _logSplitClaims.FindOneAndUpdateAsync(
                Builders<LogSplitClaimDocument>.Filter.Eq(c => c.Id, chunk.Id)
                    & Builders<LogSplitClaimDocument>.Filter.Eq(c => c.Owner, _logSplitOwner)
                    & Builders<LogSplitClaimDocument>.Filter.Eq(c => c.AttemptId, chunk.SplitAttempt)
                    & Builders<LogSplitClaimDocument>.Filter.Gte(c => c.ClaimedAt, cutoff),
                Builders<LogSplitClaimDocument>.Update
                    .Set(c => c.PublishingAttempt, chunk.SplitAttempt)
                    .Set(c => c.ClaimedAt, now)
                    .Set(c => c.SourceLineCount, chunk.LineCount),
                new FindOneAndUpdateOptions<LogSplitClaimDocument> { ReturnDocument = ReturnDocument.After },
                token),
            ct,
            _logger);
        return updated != null
            && string.Equals(updated.Owner, _logSplitOwner, StringComparison.Ordinal)
            && string.Equals(updated.PublishingAttempt, chunk.SplitAttempt, StringComparison.Ordinal)
            && string.Equals(updated.AttemptId, chunk.SplitAttempt, StringComparison.Ordinal)
            && updated.SourceLineCount == chunk.LineCount;
    }

    /// <summary>
    /// Deletes the source when it still has the line count stored with the
    /// publishing mark, then detaches this attempt's replacements. A source that
    /// is already gone is treated as published. A source that grew is left in
    /// place and the mark is cleared so the caller can split the new lines.
    /// </summary>
    private async Task<bool> FinishExternalPublishAsync(RunLogDocument chunk, int start, int count, CancellationToken ct)
    {
        if (!await SplitAttemptStillOwnedAsync(chunk, ct))
            return false;

        var claim = await _logSplitClaims.Find(c => c.Id == chunk.Id).FirstOrDefaultAsync(ct);
        if (claim == null
            || !string.Equals(claim.PublishingAttempt, chunk.SplitAttempt, StringComparison.Ordinal)
            || !string.Equals(claim.AttemptId, chunk.SplitAttempt, StringComparison.Ordinal))
        {
            return false;
        }

        // A claim marked before this field existed has no stored count. Those
        // deletes keep the in-memory count. New marks always store it.
        var storedCount = claim.SourceLineCount > 0 ? claim.SourceLineCount : chunk.LineCount;
        var live = await _logs.Find(l => l.Id == chunk.Id).FirstOrDefaultAsync(ct);
        if (live == null)
        {
            await DetachPublishedExternalSplitAsync(chunk, start, count, deletedSource: false, ct);
            return true;
        }

        if (live.LineCount != storedCount)
        {
            await ClearExternalPublishingAsync(chunk, ct);
            return false;
        }

        // Drop other attempts' copies while this attempt still owns the lease.
        // A late insert from a worker that already lost the lease must not
        // survive next to this attempt's rows.
        await ReconcileAbandonedReplacementsAsync(chunk, predecessorsOnly: true, ct);

        if (BeforeLogSourceDeleteForTests != null)
        {
            var beforeSourceDelete = BeforeLogSourceDeleteForTests;
            BeforeLogSourceDeleteForTests = null;
            await beforeSourceDelete(ct);
        }

        live = await _logs.Find(l => l.Id == chunk.Id).FirstOrDefaultAsync(ct);
        if (live == null)
        {
            await DetachPublishedExternalSplitAsync(chunk, start, count, deletedSource: false, ct);
            return true;
        }

        if (live.LineCount != storedCount)
        {
            await ClearExternalPublishingAsync(chunk, ct);
            return false;
        }

        var current = await _logSplitClaims.Find(c => c.Id == chunk.Id).FirstOrDefaultAsync(ct);
        if (current == null
            || !string.Equals(current.AttemptId, chunk.SplitAttempt, StringComparison.Ordinal)
            || !string.Equals(current.PublishingAttempt, chunk.SplitAttempt, StringComparison.Ordinal))
        {
            return false;
        }

        var deleted = await CosmosThrottle.ExecuteAsync(
            token => _logs.DeleteOneAsync(
                l => l.Id == chunk.Id && l.LineCount == storedCount,
                token),
            ct,
            _logger);
        if (deleted.DeletedCount == 1)
        {
            await DetachPublishedExternalSplitAsync(chunk, start, count, deletedSource: true, ct);
            return true;
        }

        if (!await _logs.Find(l => l.Id == chunk.Id).AnyAsync(ct))
        {
            await DetachPublishedExternalSplitAsync(chunk, start, count, deletedSource: false, ct);
            return true;
        }

        await ClearExternalPublishingAsync(chunk, ct);
        return false;
    }

    /// <summary>
    /// Marks, deletes, and, when the source grew after the mark, splits that
    /// source again. <paramref name="planned"/> is already stored.
    /// </summary>
    private async Task<bool> PublishExternalSplitAsync(
        RunLogDocument chunk,
        List<RunLogDocument> planned,
        int start,
        bool metadataFits,
        CancellationToken ct)
    {
        var current = planned;
        var currentStart = start;
        for (var pass = 0; pass < 5; pass++)
        {
            if (!await MarkExternalSplitPublishingAsync(chunk, ct))
                return false;

            if (await FinishExternalPublishAsync(chunk, currentStart, current.Count, ct))
                return true;

            var replanned = await ReplanChangedExternalSplitAsync(chunk, metadataFits, ct);
            if (replanned == null || chunk.SplitStart == null)
                return false;

            current = replanned;
            currentStart = chunk.SplitStart.Value;
            var write = await WriteSplitReplacementsAsync(chunk, current, currentStart, metadataFits, ct);
            if (!write.Wrote || write.Start == null)
                return false;

            currentStart = write.Start.Value;
            if (!await StillOwnLogSplitAsync(chunk.Id, metadataFits, ct))
                return false;
        }

        return false;
    }

    /// <summary>
    /// Reloads a source that grew after its replacements were marked, drops
    /// those replacements, and claims a range for the lines that are there now.
    /// </summary>
    private async Task<List<RunLogDocument>?> ReplanChangedExternalSplitAsync(
        RunLogDocument chunk,
        bool metadataFits,
        CancellationToken ct)
    {
        if (!await SplitAttemptStillOwnedAsync(chunk, ct))
            return null;

        var live = await _logs.Find(l => l.Id == chunk.Id).FirstOrDefaultAsync(ct);
        if (live == null || live.Lines.Count == 0)
            return null;

        chunk.Lines = live.Lines;
        chunk.LineCount = live.LineCount;
        chunk.LineSequences = live.LineSequences;
        chunk.BsonByteCount = live.BsonByteCount;
        chunk.ByteCountVersion = live.ByteCountVersion;
        await OverlaySequenceStampsAsync([chunk], ct);

        // The old attempt still owns these rows. Remove the copies built from
        // the shorter source before the new claim allocates another range.
        await ReconcileAbandonedReplacementsAsync(chunk, predecessorsOnly: false, ct);
        if (!await SplitAttemptStillOwnedAsync(chunk, ct))
            return null;

        await ClearExternalPublishingAsync(chunk, ct);
        var claim = await _logSplitClaims.Find(c => c.Id == chunk.Id).FirstOrDefaultAsync(ct);
        if (claim != null && !string.IsNullOrEmpty(claim.PublishingAttempt))
            return null;

        chunk.ResumePublishing = false;
        chunk.SplitStart = null;
        var firstSequence = await FallbackSequenceBeforeChunkAsync(chunk, ct);
        await StampFollowingUnsequencedChunksAsync(chunk, firstSequence, ct);
        var planned = PlanLogSplit(chunk, firstSequence);
        if (planned.Count == 0)
            return null;

        var claimedStart = await ClaimLogSplitStartAsync(
            chunk,
            planned.Count,
            metadataFits,
            ct,
            resumeSavedStart: false);
        if (claimedStart == null || chunk.ResumePublishing)
            return null;

        chunk.SplitStart = claimedStart;
        return planned;
    }

    private async Task ClearExternalPublishingAsync(RunLogDocument chunk, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(chunk.SplitAttempt))
            return;

        await CosmosThrottle.ExecuteAsync(
            token => _logSplitClaims.UpdateOneAsync(
                c => c.Id == chunk.Id
                    && c.Owner == _logSplitOwner
                    && c.AttemptId == chunk.SplitAttempt
                    && c.PublishingAttempt == chunk.SplitAttempt,
                Builders<LogSplitClaimDocument>.Update
                    .Unset(c => c.PublishingAttempt)
                    .Set(c => c.SourceLineCount, 0),
                cancellationToken: token),
            ct,
            _logger);
    }

    private async Task DetachPublishedExternalSplitAsync(
        RunLogDocument chunk,
        int start,
        int count,
        bool deletedSource,
        CancellationToken ct)
    {
        await CosmosThrottle.ExecuteAsync(
            token => _logSequenceStamps.DeleteOneAsync(s => s.Id == chunk.Id, token),
            ct,
            _logger);
        await CosmosThrottle.ExecuteAsync(
            token => _logSplitClaims.DeleteOneAsync(
                c => c.Id == chunk.Id
                    && c.Owner == _logSplitOwner
                    && c.PublishingAttempt == chunk.SplitAttempt,
                token),
            ct,
            _logger);

        if (deletedSource && AfterLogSourceDeletedForTests != null)
        {
            var sourceDeleted = AfterLogSourceDeletedForTests;
            AfterLogSourceDeletedForTests = null;
            await sourceDeleted(ct);
        }

        await CosmosThrottle.ExecuteAsync(
            token => _logs.UpdateManyAsync(
                l => l.SplitFromId == chunk.Id
                    && l.SplitAttempt == chunk.SplitAttempt
                    && l.ChunkNumber >= start
                    && l.ChunkNumber < start + count,
                Builders<RunLogDocument>.Update.Unset(l => l.SplitFromId),
                cancellationToken: token),
            ct,
            _logger);
    }

    /// <summary>
    /// Inserts <paramref name="planned"/> under the current attempt. A worker
    /// that loses the lease stops before it writes and does not reconcile the
    /// successor's rows.
    /// </summary>
    private async Task<(bool Wrote, int? Start)> WriteSplitReplacementsAsync(
        RunLogDocument chunk,
        List<RunLogDocument> planned,
        int rangeStart,
        bool metadataFits,
        CancellationToken ct)
    {
        int? start = rangeStart;
        var wrote = false;
        for (var attempt = 0; attempt < 5 && !wrote; attempt++)
        {
            if (BeforeLogReplacementInsertForTests != null)
            {
                var insertHook = BeforeLogReplacementInsertForTests;
                BeforeLogReplacementInsertForTests = null;
                await insertHook(ct);
            }

            if (!await SplitAttemptStillOwnedAsync(chunk, ct))
            {
                start = null;
                break;
            }

            var collided = false;
            var lostLease = false;
            for (var n = 0; n < planned.Count; n++)
            {
                if (!await SplitAttemptStillOwnedAsync(chunk, ct))
                {
                    lostLease = true;
                    break;
                }

                var document = planned[n];
                document.Id = CreateLogChunkId(chunk.RunId, start.Value + n);
                document.ChunkNumber = start.Value + n;
                document.SplitFromId = chunk.Id;
                document.SplitAttempt = chunk.SplitAttempt;
                document.UpdatedAt = DateTimeOffset.UtcNow;
                EnsureLogChunkWithinCap(document);

                var existing = await _logs.Find(l => l.Id == document.Id).FirstOrDefaultAsync(ct);
                if (existing != null && existing.SplitFromId != chunk.Id)
                {
                    collided = true;
                    break;
                }

                // A successor's row carries that attempt's id. Rewriting it
                // from this attempt's stale plan drops lines the successor
                // already kept.
                if (existing != null
                    && !string.Equals(existing.SplitAttempt, chunk.SplitAttempt, StringComparison.Ordinal))
                {
                    collided = true;
                    break;
                }

                // Same text at a different sequence is an appended event. A resume
                // that only compares text would delete the source and lose it.
                if (existing != null && !SameLogLines(existing, document))
                {
                    await PreserveAppendedReplacementAsync(existing, chunk, chunk.SplitAttempt, ct);
                    collided = true;
                    break;
                }

                if (existing == null)
                {
                    try
                    {
                        await CosmosThrottle.ExecuteAsync(
                            token => _logs.InsertOneAsync(document, cancellationToken: token),
                            ct,
                            _logger);
                    }
                    catch (Exception ex) when (IsDuplicateKey(ex))
                    {
                        collided = true;
                        break;
                    }
                }
            }

            if (lostLease)
            {
                start = null;
                break;
            }

            if (!collided)
            {
                wrote = true;
                break;
            }

            if (!await StillOwnLogSplitAsync(chunk.Id, metadataFits, ct))
            {
                start = null;
                break;
            }

            await ReconcileAbandonedReplacementsAsync(chunk, predecessorsOnly: false, ct);
            chunk.SplitStart = null;
            start = await ClaimLogSplitStartAsync(chunk, planned.Count, metadataFits, ct, resumeSavedStart: false);
            if (start == null)
                break;
        }

        return (wrote, start);
    }

    private async Task<bool> TryClaimLogSplitAsync(RunLogDocument chunk, int start, int count, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var cutoff = now - LogSplitClaimLease;
        var filter = Builders<RunLogDocument>.Filter.Eq(l => l.Id, chunk.Id)
            & Builders<RunLogDocument>.Filter.Or(
                Builders<RunLogDocument>.Filter.Exists(l => l.SplitOwner, false),
                Builders<RunLogDocument>.Filter.Eq(l => l.SplitOwner, null),
                Builders<RunLogDocument>.Filter.Eq(l => l.SplitOwner, _logSplitOwner),
                Builders<RunLogDocument>.Filter.Lt(l => l.SplitClaimedAt, cutoff));
        var attempt = Guid.NewGuid().ToString("N");
        var update = Builders<RunLogDocument>.Update
            .Set(l => l.SplitOwner, _logSplitOwner)
            .Set(l => l.SplitClaimedAt, now)
            .Set(l => l.SplitStart, start)
            .Set(l => l.SplitCount, count)
            .Set(l => l.SplitAttempt, attempt);
        var claimed = await CosmosThrottle.ExecuteAsync(
            token => _logs.FindOneAndUpdateAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<RunLogDocument> { ReturnDocument = ReturnDocument.After },
                token),
            ct,
            _logger);
        if (claimed == null || !string.Equals(claimed.SplitOwner, _logSplitOwner, StringComparison.Ordinal))
            return false;

        chunk.SplitOwner = _logSplitOwner;
        chunk.SplitStart = start;
        chunk.SplitCount = count;
        chunk.SplitClaimedAt = now;
        chunk.SplitAttempt = attempt;
        return true;
    }

    private async Task<bool> StillOwnLogSplitAsync(string chunkId, bool metadataFits, CancellationToken ct)
    {
        if (metadataFits)
        {
            var owner = await _logs.Find(l => l.Id == chunkId)
                .Project(l => l.SplitOwner)
                .FirstOrDefaultAsync(ct);
            return string.Equals(owner, _logSplitOwner, StringComparison.Ordinal);
        }

        var claim = await _logSplitClaims.Find(c => c.Id == chunkId).FirstOrDefaultAsync(ct);
        return claim != null
            && string.Equals(claim.Owner, _logSplitOwner, StringComparison.Ordinal)
            && claim.ClaimedAt >= DateTimeOffset.UtcNow - LogSplitClaimLease;
    }

    /// <summary>
    /// False when adding the byte-count version and the split-claim fields would
    /// push this chunk over the Cosmos document cap. The caller then leases the
    /// split in <see cref="LogSplitClaimDocument"/> and leaves the source unchanged.
    /// </summary>
    internal static bool LogMetadataFits(RunLogDocument chunk)
    {
        var bytes = Encoding.UTF8.GetByteCount(chunk.ToBsonDocument().ToJson());
        return bytes + LogMetadataReserveBytes <= SnapshotPartitioner.HardCapBytes;
    }

    private static bool SameSequences(IReadOnlyList<long>? left, IReadOnlyList<long>? right)
    {
        var first = left ?? [];
        var second = right ?? [];
        return first.Count == second.Count && first.SequenceEqual(second);
    }

    /// <summary>
    /// Drops unpublished replacement copies. A replacement that picked up an
    /// appended line keeps that line and is detached from the split.
    /// <paramref name="predecessorsOnly"/> is set after a new claim so this
    /// attempt leaves its own rows alone and only cleans earlier attempts.
    /// </summary>
    private async Task ReconcileAbandonedReplacementsAsync(
        RunLogDocument chunk,
        bool predecessorsOnly,
        CancellationToken ct)
    {
        if (BeforeReplacementReconcileForTests != null)
        {
            var beforeReconcile = BeforeReplacementReconcileForTests;
            BeforeReplacementReconcileForTests = null;
            await beforeReconcile(ct);
        }

        if (string.IsNullOrEmpty(chunk.SplitAttempt))
            return;

        if (!await SplitAttemptStillOwnedAsync(chunk, ct))
            return;

        var replacements = await _logs.Find(l => l.SplitFromId == chunk.Id).ToListAsync(ct);
        foreach (var existing in replacements)
        {
            var ownedAttempt = string.Equals(existing.SplitAttempt, chunk.SplitAttempt, StringComparison.Ordinal)
                || string.IsNullOrEmpty(existing.SplitAttempt);
            // A new claim cleans earlier attempts, including rows stored before
            // attempt ids existed. A retry of this claim cleans its own rows
            // and those unscoped rows, and leaves a successor's attempt alone.
            if (predecessorsOnly)
            {
                if (string.Equals(existing.SplitAttempt, chunk.SplitAttempt, StringComparison.Ordinal))
                    continue;
            }
            else if (!ownedAttempt)
            {
                continue;
            }

            if (!await SplitAttemptStillOwnedAsync(chunk, ct))
                return;

            await PreserveAppendedReplacementAsync(existing, chunk, existing.SplitAttempt, ct);
        }
    }

    /// <summary>
    /// True while the stored claim still names this attempt. A takeover changes
    /// that id, and a stale worker must stop before it touches the new rows.
    /// </summary>
    private async Task<bool> SplitAttemptStillOwnedAsync(RunLogDocument chunk, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(chunk.SplitAttempt))
            return false;

        var owner = await _logs.Find(l => l.Id == chunk.Id)
            .Project(l => new RunLogDocument { SplitOwner = l.SplitOwner, SplitAttempt = l.SplitAttempt })
            .FirstOrDefaultAsync(ct);
        if (owner != null
            && string.Equals(owner.SplitOwner, _logSplitOwner, StringComparison.Ordinal)
            && string.Equals(owner.SplitAttempt, chunk.SplitAttempt, StringComparison.Ordinal))
        {
            return true;
        }

        var claim = await _logSplitClaims.Find(c => c.Id == chunk.Id).FirstOrDefaultAsync(ct);
        return claim != null
            && string.Equals(claim.Owner, _logSplitOwner, StringComparison.Ordinal)
            && string.Equals(claim.AttemptId, chunk.SplitAttempt, StringComparison.Ordinal)
            && claim.ClaimedAt >= DateTimeOffset.UtcNow - LogSplitClaimLease;
    }

    private async Task PreserveAppendedReplacementAsync(
        RunLogDocument existing,
        RunLogDocument source,
        string? authorizedAttempt,
        CancellationToken ct)
    {
        var normalized = new List<string>(source.Lines.Count);
        foreach (var line in source.Lines)
            normalized.Add(NormalizeLineForChunkBudget(source.RunId, line));

        var fallback = await FallbackSequenceBeforeChunkAsync(source, ct);
        var copied = ContiguousSourcePrefixLength(
            existing.Lines,
            existing.LineSequences,
            normalized,
            EffectiveLogSequences(source, fallback));
        var extras = new List<string>();
        var sequences = new List<long>();
        var storedSequences = existing.LineSequences ?? [];
        for (var i = copied; i < existing.Lines.Count; i++)
        {
            extras.Add(existing.Lines[i]);
            if (i < storedSequences.Count)
                sequences.Add(storedSequences[i]);
        }

        if (extras.Count == 0)
        {
            await CosmosThrottle.ExecuteAsync(
                token => _logs.DeleteOneAsync(
                    l => l.Id == existing.Id
                        && l.SplitFromId == source.Id
                        && l.SplitAttempt == authorizedAttempt,
                    token),
                ct,
                _logger);
            return;
        }

        var bytes = EstimateChunkLinesBsonBytes(extras);
        await CosmosThrottle.ExecuteAsync(
            token => _logs.UpdateOneAsync(
                l => l.Id == existing.Id
                    && l.SplitFromId == source.Id
                    && l.SplitAttempt == existing.SplitAttempt,
                Builders<RunLogDocument>.Update
                    .Set(l => l.Lines, extras)
                    .Set(l => l.LineSequences, sequences)
                    .Set(l => l.LineCount, extras.Count)
                    .Set(l => l.BsonByteCount, bytes)
                    .Set(l => l.ByteCountVersion, EscapedLogByteCountVersion)
                    .Unset(l => l.SplitFromId),
                cancellationToken: token),
            ct,
            _logger);
    }

    private static int ContiguousSourcePrefixLength(
        IReadOnlyList<string> existing,
        IReadOnlyList<long>? existingSequences,
        IReadOnlyList<string> normalized,
        IReadOnlyList<long>? sourceSequences)
    {
        var existingSeq = existingSequences ?? [];
        var sourceSeq = sourceSequences ?? [];
        var best = 0;
        for (var start = 0; start < normalized.Count; start++)
        {
            var length = 0;
            while (start + length < normalized.Count
                   && length < existing.Count
                   && string.Equals(existing[length], normalized[start + length], StringComparison.Ordinal))
            {
                // Same text at a different sequence is an appended event, not a copy.
                if (length < existingSeq.Count
                    && start + length < sourceSeq.Count
                    && existingSeq[length] != sourceSeq[start + length])
                {
                    break;
                }

                length++;
            }

            if (length > best)
                best = length;
        }

        return best;
    }

    private static bool SameLogLines(RunLogDocument left, RunLogDocument right)
        => left.Lines.Count == right.Lines.Count
            && left.Lines.SequenceEqual(right.Lines)
            && SameSequences(left.LineSequences, right.LineSequences);

    private async Task<bool> LogSplitRangeCollidesAsync(
        RunLogDocument chunk,
        int start,
        int count,
        CancellationToken ct)
    {
        for (var n = 0; n < count; n++)
        {
            var id = CreateLogChunkId(chunk.RunId, start + n);
            if (id == chunk.Id)
                return true;

            var existing = await _logs.Find(l => l.Id == id).FirstOrDefaultAsync(ct);
            if (existing != null && existing.SplitFromId != chunk.Id)
                return true;
        }

        return false;
    }

    private static bool IsDuplicateKey(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            switch (current)
            {
                case MongoWriteException write when write.WriteError?.Code is 11000 or 11001:
                    return true;
                case MongoCommandException command when command.Code is 11000 or 11001:
                    return true;
            }
        }

        return false;
    }

    private async Task<DomainSnapshotDocument?> ReplaceHeaderAsync(
        FilterDefinition<DomainSnapshotDocument> filter,
        ObjectId headerId,
        Guid runId,
        string domain,
        string data,
        bool upsert,
        CancellationToken ct,
        DateTimeOffset? preserveUpdatedAt = null)
    {
        if (BeforeSnapshotHeaderWriteForTests != null)
        {
            var beforeWrite = BeforeSnapshotHeaderWriteForTests;
            BeforeSnapshotHeaderWriteForTests = null;
            await beforeWrite(ct);
        }

        // Migration keeps the timestamp it read. A newer duplicate written while
        // this publish was in progress must stay newer than the migrated header.
        var update = Builders<DomainSnapshotDocument>.Update
            .Set(d => d.Data, data)
            .Set(d => d.UpdatedAt, preserveUpdatedAt ?? DateTimeOffset.UtcNow)
            .Set(d => d.Revision, NewSnapshotRevision())
            .SetOnInsert(d => d.Id, headerId)
            .SetOnInsert(d => d.RunId, runId)
            .SetOnInsert(d => d.Domain, domain);
        var options = new FindOneAndUpdateOptions<DomainSnapshotDocument>
        {
            IsUpsert = upsert,
            ReturnDocument = ReturnDocument.Before
        };
        return await CosmosThrottle.ExecuteAsync(
            async token => (DomainSnapshotDocument?)await _snapshots.FindOneAndUpdateAsync(filter, update, options, token),
            ct,
            _logger);
    }

    private async Task<bool> CommitPartitionAsync(
        ObjectId headerId,
        bool upsert,
        Guid runId,
        string domain,
        SnapshotPlan.Partitioned partitioned,
        CancellationToken ct,
        FilterDefinition<DomainSnapshotDocument>? compareAndSwap = null,
        DateTimeOffset? preserveUpdatedAt = null)
    {
        var headerFilter = compareAndSwap
            ?? Builders<DomainSnapshotDocument>.Filter.Eq(d => d.Id, headerId);
        var headerPublished = false;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            var generationId = Guid.NewGuid().ToString("N");
            var headerJson = SnapshotPartitioner.BuildHeaderJson(
                generationId,
                partitioned.Mode,
                partitioned.Pieces.Count,
                partitioned.SkeletonJson);
            SnapshotPartitioner.EnsureWithinHardCap(headerJson);
            await WritePartsAsync(runId, domain, generationId, partitioned.Pieces, ct);
            if (BeforeSnapshotHeaderWriteForTests != null)
            {
                var beforeWrite = BeforeSnapshotHeaderWriteForTests;
                BeforeSnapshotHeaderWriteForTests = null;
                await beforeWrite(ct);
            }

            if (BeforePartitionHeaderPublishForTests != null)
                await BeforePartitionHeaderPublishForTests(ct);

            // A sweep can remove parts that aged out before the header exists.
            // Publishing after that would replace a readable snapshot with a
            // header whose parts are already gone.
            if (!await GenerationPartsPresentAsync(runId, domain, generationId, partitioned.Pieces.Count, ct))
            {
                await DeleteGenerationAsync(runId, domain, generationId, ct);
                continue;
            }

            // The retry matches this revision, not the header JSON. A header near
            // 1 MB plus the same JSON in the filter would cross the 2 MB request cap.
            // UpdatedAt can be shared by two writers, so it is not the match.
            var publishedRevision = NewSnapshotRevision();
            var update = Builders<DomainSnapshotDocument>.Update
                .Set(d => d.Data, headerJson)
                .Set(d => d.UpdatedAt, preserveUpdatedAt ?? DateTimeOffset.UtcNow)
                .Set(d => d.Revision, publishedRevision)
                .SetOnInsert(d => d.Id, headerId)
                .SetOnInsert(d => d.RunId, runId)
                .SetOnInsert(d => d.Domain, domain);
            var options = new FindOneAndUpdateOptions<DomainSnapshotDocument>
            {
                // A retry matches the header this attempt already published. Upserting
                // on a miss would insert a second document with the same id.
                // An attempt that never published, because its parts were gone,
                // is still the first write.
                IsUpsert = !headerPublished && compareAndSwap == null && upsert,
                ReturnDocument = ReturnDocument.Before
            };
            var before = await CosmosThrottle.ExecuteAsync(
                token => _snapshots.FindOneAndUpdateAsync(headerFilter, update, options, token),
                ct,
                _logger);
            // A first upsert has no previous document, so Before is null on success.
            // A missed compare-and-swap, or a later attempt whose published header
            // was replaced, also returns null. Leave that newer header alone.
            // A pre-publish retry has not published yet, so a null Before is the
            // first upsert, not a lost header.
            if (before == null && (compareAndSwap != null || headerPublished))
            {
                await DeleteGenerationAsync(runId, domain, generationId, ct);
                return false;
            }

            headerPublished = true;

            if (AfterPartitionHeaderPublishForTests != null)
            {
                var afterPublish = AfterPartitionHeaderPublishForTests;
                AfterPartitionHeaderPublishForTests = null;
                await afterPublish(ct);
            }

            // A sweep that already decided these parts were old must not match
            // them after the header publish. The following count check fails
            // when the sweep deleted them first, and this attempt retries.
            await TouchGenerationAsync(runId, domain, generationId, ct);

            var previousGeneration = GenerationIdOf(before?.Data);
            if (await GenerationIsCommittedAsync(headerId, runId, domain, generationId, ct))
            {
                // Delete only the generation this commit replaced. A writer that
                // commits after us has a different generation id, and deleting
                // every other generation would remove that writer's parts.
                if (previousGeneration != null)
                    await RetireGenerationAsync(runId, domain, previousGeneration, ct);

                if (await GenerationIsCommittedAsync(headerId, runId, domain, generationId, ct))
                {
                    await MarkGenerationSettledAsync(runId, domain, generationId, ct);
                    return true;
                }
            }

            // A later commit retires the header it replaced, which is this
            // attempt, and does not know about the generation before that.
            // Retire it when the header has already moved on. Leave it when
            // this attempt is still the header, or when the publish missed.
            await RetireSupersededPreviousGenerationAsync(
                headerId, runId, domain, generationId, previousGeneration, ct);

            // The header we just published is incomplete because its parts were
            // removed. The next attempt may replace that revision and no other.
            headerFilter = Builders<DomainSnapshotDocument>.Filter.Eq(d => d.Id, headerId)
                & Builders<DomainSnapshotDocument>.Filter.Eq(d => d.Revision, publishedRevision);
            await DeleteGenerationAsync(runId, domain, generationId, ct);
        }

        throw new InvalidOperationException(
            $"Could not commit a consistent snapshot for domain '{domain}'.");
    }

    private async Task WritePartsAsync(
        Guid runId,
        string domain,
        string generationId,
        IReadOnlyList<SnapshotPiece> pieces,
        CancellationToken ct)
    {
        const int maxBatchCount = 100;
        var writes = new List<WriteModel<SnapshotPartDocument>>();
        var batchBytes = 0;
        var batchStamp = DateTimeOffset.UtcNow;

        async Task FlushAsync()
        {
            if (writes.Count == 0)
                return;

            var pending = writes;
            writes = new List<WriteModel<SnapshotPartDocument>>();
            batchBytes = 0;
            batchStamp = DateTimeOffset.UtcNow;
            await CosmosThrottle.ExecuteAsync(
                token => _parts.BulkWriteAsync(pending, new BulkWriteOptions { IsOrdered = true }, token),
                ct,
                _logger);
        }

        for (var ordinal = 0; ordinal < pieces.Count; ordinal++)
        {
            var piece = pieces[ordinal];
            var itemKey = SnapshotPartitioner.FitItemKey(
                piece.ItemKey,
                piece.Data,
                piece.Path,
                SnapshotPartitioner.MaxDocumentJsonBytes);
            if (SnapshotPartitioner.EstimatePieceBytes(piece.Data, piece.Path, itemKey) > SnapshotPartitioner.HardCapBytes)
            {
                throw new SnapshotDocumentTooLargeException(
                    $"Snapshot part path and payload are estimated over the {SnapshotPartitioner.HardCapBytes} byte Cosmos limit.");
            }

            var estimate = SnapshotPartitioner.EstimatePieceBytes(piece.Data, piece.Path, itemKey);
            if (writes.Count > 0
                && (writes.Count >= maxBatchCount || batchBytes + estimate > MaxBulkWriteJsonBytes))
            {
                await FlushAsync();
            }

            var document = new SnapshotPartDocument
            {
                Id = SnapshotPartId(runId, domain, generationId, ordinal),
                RunId = runId,
                Domain = domain,
                GenerationId = generationId,
                Ordinal = ordinal,
                Kind = piece.Kind,
                Path = piece.Path,
                Index = piece.Index,
                Slice = piece.Slice,
                ItemKey = itemKey,
                Data = piece.Data,
                UpdatedAt = batchStamp,
                Settled = false
            };
            writes.Add(new ReplaceOneModel<SnapshotPartDocument>(
                Builders<SnapshotPartDocument>.Filter.Eq(p => p.Id, document.Id),
                document)
            {
                IsUpsert = true
            });
            batchBytes += estimate;
        }

        await FlushAsync();
    }

    private async Task<List<SnapshotPiece>> LoadCommittedPiecesAsync(
        Guid runId,
        string domain,
        SnapshotPartitionHeader header,
        CancellationToken ct)
    {
        var pieces = new List<SnapshotPiece>(header.PartCount);
        var ordinal = 0;
        while (pieces.Count < header.PartCount)
        {
            var batch = await _parts.Find(p => p.RunId == runId
                    && p.Domain == domain
                    && p.GenerationId == header.GenerationId
                    && p.Ordinal >= ordinal)
                .SortBy(p => p.Ordinal)
                .Limit(200)
                .ToListAsync(ct);
            if (batch.Count == 0)
                break;

            foreach (var document in batch)
            {
                if (document.Ordinal != pieces.Count)
                    return pieces;

                pieces.Add(new SnapshotPiece(
                    document.Kind,
                    document.Path,
                    document.Index,
                    document.Slice,
                    document.Data,
                    document.ItemKey));
            }

            ordinal = batch[^1].Ordinal + 1;
        }

        return pieces;
    }

    private Task DeletePartsAsync(Guid runId, CancellationToken ct)
        => CosmosThrottle.ExecuteAsync(
            token => _parts.DeleteManyAsync(p => p.RunId == runId, token),
            ct,
            _logger);

    /// <summary>
    /// Deletes the generation the inline header write replaced. A later commit
    /// uses a new generation id, so retiring this one leaves that commit's
    /// parts in place. Retirement is skipped only when this generation is
    /// still the header.
    /// </summary>
    private async Task DeleteReplacedGenerationAsync(
        ObjectId headerId,
        DomainSnapshotDocument? before,
        CancellationToken ct)
    {
        var previousGeneration = GenerationIdOf(before?.Data);
        if (previousGeneration == null || before == null)
            return;

        if (BeforeInlineGenerationRetireForTests != null)
        {
            var beforeRetire = BeforeInlineGenerationRetireForTests;
            BeforeInlineGenerationRetireForTests = null;
            await beforeRetire(ct);
        }

        var current = await _snapshots.Find(d => d.Id == headerId).FirstOrDefaultAsync(ct);
        // The generation id is unique. Once this write replaced it, a later
        // header cannot publish it again, so retiring it cannot remove live parts.
        if (string.Equals(GenerationIdOf(current?.Data), previousGeneration, StringComparison.Ordinal))
            return;

        await RetireGenerationAsync(before.RunId, before.Domain, previousGeneration, ct);
    }

    /// <summary>
    /// Retires <paramref name="previousGeneration"/> when a later header has
    /// already replaced both it and this attempt. The later writer retires
    /// this attempt only. Skipping here leaves the older settled parts behind.
    /// </summary>
    private async Task RetireSupersededPreviousGenerationAsync(
        ObjectId headerId,
        Guid runId,
        string domain,
        string ourGeneration,
        string? previousGeneration,
        CancellationToken ct)
    {
        if (previousGeneration == null)
            return;

        var current = await _snapshots.Find(d => d.Id == headerId).FirstOrDefaultAsync(ct);
        var currentGeneration = GenerationIdOf(current?.Data);
        if (string.Equals(currentGeneration, previousGeneration, StringComparison.Ordinal)
            || string.Equals(currentGeneration, ourGeneration, StringComparison.Ordinal))
        {
            return;
        }

        await RetireGenerationAsync(runId, domain, previousGeneration, ct);
    }

    private async Task RetireGenerationAsync(Guid runId, string domain, string generationId, CancellationToken ct)
    {
        var tombstone = new RetiredSnapshotGenerationDocument
        {
            Id = $"{runId:N}:{domain}:{generationId}",
            RunId = runId,
            Domain = domain,
            GenerationId = generationId
        };
        await CosmosThrottle.ExecuteAsync(
            token => _retiredGenerations.ReplaceOneAsync(
                t => t.Id == tombstone.Id,
                tombstone,
                new ReplaceOptions { IsUpsert = true },
                token),
            ct,
            _logger);
        await DeleteGenerationAsync(runId, domain, generationId, ct);
        await CosmosThrottle.ExecuteAsync(
            token => _retiredGenerations.DeleteOneAsync(t => t.Id == tombstone.Id, token),
            ct,
            _logger);
    }

    /// <summary>
    /// Finishes generation deletes that a writer recorded and then did not
    /// complete. A tombstone whose generation is still the header is left in
    /// place so a later commit can remove it.
    /// </summary>
    internal async Task SweepRetiredGenerationsAsync(CancellationToken ct)
    {
        var batch = await _retiredGenerations
            .Find(FilterDefinition<RetiredSnapshotGenerationDocument>.Empty)
            .Limit(8)
            .ToListAsync(ct);
        foreach (var item in batch)
        {
            ct.ThrowIfCancellationRequested();
            var header = await ChooseHeaderAsync(item.RunId, item.Domain, ct);
            var current = GenerationIdOf(header?.Data);
            if (string.Equals(current, item.GenerationId, StringComparison.Ordinal))
                continue;

            await DeleteGenerationAsync(item.RunId, item.Domain, item.GenerationId, ct);
            await CosmosThrottle.ExecuteAsync(
                token => _retiredGenerations.DeleteOneAsync(t => t.Id == item.Id, token),
                ct,
                _logger);
        }
    }

    private static string NewSnapshotRevision() => Guid.NewGuid().ToString("N");

    /// <summary>
    /// Matches the snapshot that was read. A stored revision is enough, because
    /// this build changes it on every write. A missing revision is not, so the
    /// timestamp read with it has to match too.
    /// </summary>
    private static FilterDefinition<DomainSnapshotDocument> ObservedSnapshot(DomainSnapshotDocument legacy)
    {
        var filter = Builders<DomainSnapshotDocument>.Filter.Eq(d => d.Id, legacy.Id)
            & SameRevision(legacy.Revision);
        if (string.IsNullOrEmpty(legacy.Revision))
        {
            filter &= Builders<DomainSnapshotDocument>.Filter.Eq(d => d.UpdatedAt, legacy.UpdatedAt);
        }

        return filter;
    }

    /// <summary>
    /// A missing or empty revision is a document written before the field
    /// existed. A stored revision matches only that exact value.
    /// </summary>
    private static FilterDefinition<DomainSnapshotDocument> SameRevision(string? revision)
    {
        if (!string.IsNullOrEmpty(revision))
            return Builders<DomainSnapshotDocument>.Filter.Eq(d => d.Revision, revision);

        return Builders<DomainSnapshotDocument>.Filter.Or(
            Builders<DomainSnapshotDocument>.Filter.Exists(d => d.Revision, false),
            Builders<DomainSnapshotDocument>.Filter.Eq(d => d.Revision, string.Empty),
            Builders<DomainSnapshotDocument>.Filter.Eq(d => d.Revision, (string?)null));
    }

    private async Task<bool> GenerationPartsPresentAsync(
        Guid runId,
        string domain,
        string generationId,
        int partCount,
        CancellationToken ct)
    {
        var count = await CosmosThrottle.ExecuteAsync(
            token => _parts.CountDocumentsAsync(
                p => p.RunId == runId
                    && p.Domain == domain
                    && p.GenerationId == generationId
                    && p.Ordinal < partCount,
                cancellationToken: token),
            ct,
            _logger);
        return count == partCount;
    }

    private static string? GenerationIdOf(string? data)
        => SnapshotPartitionHeader.TryRead(data, out var header) && header != null
            ? header.GenerationId
            : null;

    private Task DeleteGenerationAsync(Guid runId, string domain, string generationId, CancellationToken ct)
        => CosmosThrottle.ExecuteAsync(
            token => _parts.DeleteManyAsync(
                p => p.RunId == runId && p.Domain == domain && p.GenerationId == generationId,
                token),
            ct,
            _logger);

    private Task MarkGenerationSettledAsync(Guid runId, string domain, string generationId, CancellationToken ct)
        => CosmosThrottle.ExecuteAsync(
            token => _parts.UpdateManyAsync(
                p => p.RunId == runId && p.Domain == domain && p.GenerationId == generationId,
                Builders<SnapshotPartDocument>.Update.Set(p => p.Settled, true),
                cancellationToken: token),
            ct,
            _logger);

    private Task TouchGenerationAsync(Guid runId, string domain, string generationId, CancellationToken ct)
        => CosmosThrottle.ExecuteAsync(
            token => _parts.UpdateManyAsync(
                p => p.RunId == runId && p.Domain == domain && p.GenerationId == generationId,
                Builders<SnapshotPartDocument>.Update.Set(p => p.UpdatedAt, DateTimeOffset.UtcNow),
                cancellationToken: token),
            ct,
            _logger);

    private async Task<bool> GenerationIsCommittedAsync(
        ObjectId headerId,
        Guid runId,
        string domain,
        string generationId,
        CancellationToken ct)
    {
        var committed = await _snapshots.Find(d => d.Id == headerId).FirstOrDefaultAsync(ct);
        if (!SnapshotPartitionHeader.TryRead(committed?.Data, out var header)
            || header == null
            || !string.Equals(header.GenerationId, generationId, StringComparison.Ordinal))
        {
            return false;
        }

        var count = await CosmosThrottle.ExecuteAsync(
            token => _parts.CountDocumentsAsync(
                p => p.RunId == runId
                    && p.Domain == domain
                    && p.GenerationId == generationId
                    && p.Ordinal < header.PartCount,
                cancellationToken: token),
            ct,
            _logger);
        return count == header.PartCount;
    }

    private DomainSnapshot<T>? DeserializeDomain<T>(Guid runId, string domain, DomainSnapshotDocument doc, string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            _logger.LogDebug("[Store] GetDomain: empty payload for run={RunId} domain={Domain}", runId.ToString().SanitizeForLog(), domain.SanitizeForLog());
            return null;
        }

        var data = JsonSerializer.Deserialize<T>(payloadJson);
        if (data == null)
        {
            _logger.LogDebug("[Store] GetDomain: deserialized to null for run={RunId} domain={Domain} (json length={Len})", runId.ToString().SanitizeForLog(), domain.SanitizeForLog(), payloadJson.Length);
            return null;
        }

        return new DomainSnapshot<T> { UpdatedAt = doc.UpdatedAt, Data = data };
    }

    internal static string SnapshotPartId(Guid runId, string domain, string generationId, int ordinal)
        => $"{runId:N}:{domain}:{generationId}:{ordinal:D8}";

    /// <summary>
    /// Stable header id for a run and domain. New snapshots upsert this id so
    /// two first writes cannot insert two documents. Existing ObjectId headers
    /// are updated in place.
    /// </summary>
    internal static ObjectId CanonicalSnapshotId(Guid runId, string domain)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(runId.ToString("N") + "\0" + domain));
        var id = new byte[12];
        hash.AsSpan(0, 12).CopyTo(id);
        return new ObjectId(id);
    }

    internal Task<DomainSnapshotDocument?> ReadHeaderAsync(Guid runId, string domain, CancellationToken ct)
        => ChooseHeaderAsync(runId, domain, ct);

    private async Task<DomainSnapshotDocument?> ChooseHeaderAsync(Guid runId, string domain, CancellationToken ct)
    {
        var matches = await _snapshots.Find(d => d.RunId == runId && d.Domain == domain)
            .ToListAsync(ct);
        if (matches.Count == 0)
            return null;

        var canonical = CanonicalSnapshotId(runId, domain);
        return matches
            .OrderByDescending(d => d.UpdatedAt)
            .ThenByDescending(d => d.Id == canonical)
            .ThenByDescending(d => d.Id)
            .First();
    }

    private async Task<HeaderWrite> ResolveHeaderWriteAsync(Guid runId, string domain, CancellationToken ct)
    {
        var matches = await _snapshots.Find(d => d.RunId == runId && d.Domain == domain)
            .ToListAsync(ct);
        if (matches.Count == 0)
        {
            var created = CanonicalSnapshotId(runId, domain);
            return new HeaderWrite(
                Builders<DomainSnapshotDocument>.Filter.Eq(d => d.Id, created),
                created,
                true,
                null);
        }

        var chosen = await ChooseHeaderAsync(runId, domain, ct);
        // Upsert the chosen id. A concurrent cleanup can delete it after this
        // resolve, and an update-only write would then match nothing.
        return new HeaderWrite(
            Builders<DomainSnapshotDocument>.Filter.Eq(d => d.Id, chosen!.Id),
            chosen.Id,
            true,
            chosen);
    }

    /// <summary>
    /// Removes headers that lose to <paramref name="keptId"/>. A newer timestamp
    /// wins, then the canonical id, then the greater id. A tie is not deleted
    /// by the loser, so two cleanups cannot erase both snapshots.
    /// </summary>
    internal async Task DeleteStaleHeadersAsync(Guid runId, string domain, ObjectId keptId, CancellationToken ct)
    {
        var kept = await _snapshots.Find(d => d.Id == keptId).FirstOrDefaultAsync(ct);
        if (kept == null)
            return;

        var canonical = CanonicalSnapshotId(runId, domain);
        var sameDomain = Builders<DomainSnapshotDocument>.Filter.Eq(d => d.RunId, runId)
            & Builders<DomainSnapshotDocument>.Filter.Eq(d => d.Domain, domain)
            & Builders<DomainSnapshotDocument>.Filter.Ne(d => d.Id, keptId);
        // The canonical id wins a timestamp tie. Any other kept id deletes only
        // a strictly older header, or a tie whose id is lower and not canonical.
        var loses = kept.Id == canonical
            ? Builders<DomainSnapshotDocument>.Filter.Lte(d => d.UpdatedAt, kept.UpdatedAt)
            : Builders<DomainSnapshotDocument>.Filter.Lt(d => d.UpdatedAt, kept.UpdatedAt)
                | (Builders<DomainSnapshotDocument>.Filter.Eq(d => d.UpdatedAt, kept.UpdatedAt)
                    & Builders<DomainSnapshotDocument>.Filter.Lt(d => d.Id, kept.Id)
                    & Builders<DomainSnapshotDocument>.Filter.Ne(d => d.Id, canonical));

        await CosmosThrottle.ExecuteAsync(
            token => _snapshots.DeleteManyAsync(sameDomain & loses, token),
            ct,
            _logger);
    }

    private readonly record struct HeaderWrite(
        FilterDefinition<DomainSnapshotDocument> Filter,
        ObjectId HeaderId,
        bool Upsert,
        DomainSnapshotDocument? Existing);

    private static SnapshotPayloadPointer? TryReadSnapshotPayloadPointer(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            if (!doc.RootElement.TryGetProperty(SnapshotPayloadPointerEnvelopeProperty, out var pointerElement)
                || pointerElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var pointer = pointerElement.Deserialize<SnapshotPayloadPointer>();
            if (pointer == null)
                return null;

            if (!string.Equals(pointer.Kind, SnapshotPayloadPointer.KindValue, StringComparison.OrdinalIgnoreCase))
                return null;

            if (string.IsNullOrWhiteSpace(pointer.BlobName))
                return null;

            return pointer;
        }
        catch
        {
            return null;
        }
    }

    private sealed class InlineSnapshotPayloadStore : ISnapshotPayloadStore
    {
        public bool ShouldExternalize(string domain, int payloadUtf8Bytes) => false;

        public Task<SnapshotPayloadPointer> StoreAsync(Guid runId, string domain, string payloadJson, CancellationToken ct = default)
            => throw new NotSupportedException("Inline snapshot payload store does not externalize payloads.");

        public Task<string?> ReadAsync(SnapshotPayloadPointer pointer, CancellationToken ct = default)
            => Task.FromResult<string?>(null);

        public Task DeleteIfExistsAsync(SnapshotPayloadPointer pointer, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task DeleteRunPayloadsAsync(Guid runId, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    // --- Logs ---

    public async Task AppendLogsAsync(Guid runId, IReadOnlyList<string> newLines, CancellationToken ct = default)
    {
        if (newLines.Count == 0)
            return;

        var nextLineSequence = await ReserveLogSequenceRangeAsync(runId, newLines.Count, ct);

        foreach (var rawLine in newLines)
        {
            var lineSequence = nextLineSequence++;
            var line = NormalizeLineForChunkBudget(runId, rawLine);
            var lineEstimatedBsonBytes = EstimateLogLineBsonBytes(line);

            while (true)
            {
                var currentChunk = await FindAppendTargetAsync(runId, ct);

                if (currentChunk != null && currentChunk.ByteCountVersion != EscapedLogByteCountVersion)
                {
                    // A near-cap legacy chunk cannot grow to record the new byte-count
                    // version. Leave it unchanged and append onto a new chunk.
                    if (!LogMetadataFits(currentChunk))
                    {
                        currentChunk = null;
                    }
                }

                if (currentChunk != null && currentChunk.ByteCountVersion != EscapedLogByteCountVersion)
                {
                    var estimatedChunkBsonBytes = EstimateChunkLinesBsonBytes(currentChunk.Lines);
                    var recomputeFilter = Builders<RunLogDocument>.Filter.And(
                        Builders<RunLogDocument>.Filter.Eq(l => l.Id, currentChunk.Id),
                        Builders<RunLogDocument>.Filter.Eq(l => l.LineCount, currentChunk.LineCount),
                        Builders<RunLogDocument>.Filter.Ne(l => l.ByteCountVersion, EscapedLogByteCountVersion));
                    var recompute = await _logs.UpdateOneAsync(
                        recomputeFilter,
                        Builders<RunLogDocument>.Update
                            .Set(l => l.BsonByteCount, estimatedChunkBsonBytes)
                            .Set(l => l.ByteCountVersion, EscapedLogByteCountVersion),
                        cancellationToken: ct);
                    if (recompute.ModifiedCount != 1)
                        continue;

                    currentChunk.BsonByteCount = estimatedChunkBsonBytes;
                    currentChunk.ByteCountVersion = EscapedLogByteCountVersion;
                }

                var currentChunkEstimatedBsonBytes = currentChunk?.BsonByteCount ?? 0;
                if (currentChunk == null
                    || currentChunk.LineCount >= MaxLogLinesPerChunk
                    || currentChunkEstimatedBsonBytes + lineEstimatedBsonBytes > MaxLogChunkEstimatedBsonBytes
                    || (currentChunk.LineSequences?.Count ?? 0) != currentChunk.LineCount)
                {
                    var nextChunkNumber = await NextAppendChunkNumberAsync(runId, ct);
                    var nextChunk = new RunLogDocument
                    {
                        Id = CreateLogChunkId(runId, nextChunkNumber),
                        RunId = runId,
                        ChunkNumber = nextChunkNumber,
                        LineCount = 1,
                        BsonByteCount = lineEstimatedBsonBytes,
                        ByteCountVersion = EscapedLogByteCountVersion,
                        Lines = [line],
                        LineSequences = [lineSequence],
                        UpdatedAt = DateTimeOffset.UtcNow
                    };

                    try
                    {
                        EnsureLogChunkWithinCap(nextChunk);
                        await _logs.InsertOneAsync(nextChunk, cancellationToken: ct);
                        break;
                    }
                    catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
                    {
                        // Another concurrent logger created this chunk first. Re-read and append to it.
                    }

                    continue;
                }

                var filter = Builders<RunLogDocument>.Filter.And(
                    Builders<RunLogDocument>.Filter.Eq(l => l.Id, currentChunk.Id),
                    Builders<RunLogDocument>.Filter.Lt(l => l.LineCount, MaxLogLinesPerChunk),
                    Builders<RunLogDocument>.Filter.Lte(l => l.BsonByteCount, MaxLogChunkEstimatedBsonBytes - lineEstimatedBsonBytes),
                    Builders<RunLogDocument>.Filter.Eq(l => l.SplitStart, null),
                    Builders<RunLogDocument>.Filter.Eq(l => l.SplitFromId, null));
                var update = Builders<RunLogDocument>.Update
                    .Push(l => l.Lines, line)
                    .Push(l => l.LineSequences, lineSequence)
                    .Inc(l => l.LineCount, 1)
                    .Inc(l => l.BsonByteCount, lineEstimatedBsonBytes)
                    .Set(l => l.UpdatedAt, DateTimeOffset.UtcNow);

                var result = await _logs.UpdateOneAsync(filter, update, cancellationToken: ct);
                if (result.ModifiedCount == 1)
                    break;
            }
        }
    }

    private async Task<long> ReserveLogSequenceRangeAsync(Guid runId, int lineCount, CancellationToken ct)
    {
        await EnsureLogSequenceCounterInitializedAsync(runId, ct);

        var update = Builders<RunLogSequenceDocument>.Update.Inc(s => s.NextSequence, lineCount);
        var updated = await _logSequences.FindOneAndUpdateAsync(
            s => s.RunId == runId,
            update,
            new FindOneAndUpdateOptions<RunLogSequenceDocument>
            {
                ReturnDocument = ReturnDocument.After
            },
            ct);

        return updated.NextSequence - lineCount;
    }

    private async Task EnsureLogSequenceCounterInitializedAsync(Guid runId, CancellationToken ct)
    {
        var existing = await _logSequences.Find(s => s.RunId == runId).AnyAsync(ct);
        if (existing)
            return;

        var legacyLog = await _logs.Find(l => l.Id == runId.ToString()).FirstOrDefaultAsync(ct);
        var chunks = await _logs.Find(CreateLogChunkFilter(runId)).ToListAsync(ct);
        var existingIds = new HashSet<string>(StringComparer.Ordinal);
        if (legacyLog != null)
            existingIds.Add(legacyLog.Id);
        foreach (var chunk in chunks)
            existingIds.Add(chunk.Id);
        var existingLineCount = (legacyLog?.Lines.Count ?? 0)
            + chunks.Where(c => !IsPendingSplitReplacement(c.SplitFromId, existingIds)).Sum(c => c.LineCount);

        try
        {
            await _logSequences.InsertOneAsync(new RunLogSequenceDocument
            {
                RunId = runId,
                NextSequence = existingLineCount
            }, cancellationToken: ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // Another concurrent append initialized the counter first.
        }
    }

    private string NormalizeLineForChunkBudget(Guid runId, string line)
    {
        if (EstimateLogLineBsonBytes(line) <= MaxLogChunkEstimatedBsonBytes)
            return line;

        var suffixBytes = SnapshotPartitioner.EscapedContentBytes(OversizedLogLineSuffix);
        var contentBudget = MaxLogChunkEstimatedBsonBytes - EstimatedBsonBytesPerLineOverhead - suffixBytes;
        var keptBytes = 0;
        var builder = new StringBuilder();
        foreach (var rune in line.EnumerateRunes())
        {
            var runeText = rune.ToString();
            var runeBytes = SnapshotPartitioner.EscapedContentBytes(runeText);
            if (keptBytes + runeBytes > contentBudget)
                break;

            builder.Append(runeText);
            keptBytes += runeBytes;
        }

        builder.Append(OversizedLogLineSuffix);
        var persisted = builder.ToString();
        _logger.LogWarning(
            "[Store] AppendLogs: truncated oversized log line for run={RunId} from {OriginalBytes} to {PersistedBytes} bytes",
            runId.ToString().SanitizeForLog(),
            Encoding.UTF8.GetByteCount(line),
            Encoding.UTF8.GetByteCount(persisted));

        return persisted;
    }

    private static int EstimateLogLineBsonBytes(string line)
        => SnapshotPartitioner.EscapedContentBytes(line) + EstimatedBsonBytesPerLineOverhead;

    private static void EnsureLogChunkWithinCap(RunLogDocument chunk)
    {
        var json = JsonSerializer.Serialize(chunk);
        if (Encoding.UTF8.GetByteCount(json) > SnapshotPartitioner.HardCapBytes)
        {
            throw new SnapshotDocumentTooLargeException(
                $"Run log chunk is {Encoding.UTF8.GetByteCount(json)} UTF-8 bytes, over the {SnapshotPartitioner.HardCapBytes} byte Cosmos limit.");
        }
    }

    private static int EstimateChunkLinesBsonBytes(IReadOnlyList<string> lines)
    {
        var total = 0;
        foreach (var chunkLine in lines)
            total += EstimateLogLineBsonBytes(chunkLine);

        return total;
    }

    public async Task<List<string>> GetLogsAsync(Guid runId, CancellationToken ct = default)
    {
        // A split can delete the legacy document and publish its replacements
        // between these two reads. The in-memory legacy copy would then be
        // returned again next to the published chunks. Read both again.
        const int attempts = 3;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var legacyLog = await _logs.Find(l => l.Id == runId.ToString()).FirstOrDefaultAsync(ct);
            if (AfterLegacyLogReadForTests != null)
            {
                var afterLegacyRead = AfterLegacyLogReadForTests;
                AfterLegacyLogReadForTests = null;
                await afterLegacyRead(ct);
            }

            var chunks = await _logs.Find(CreateLogChunkFilter(runId))
                .SortBy(l => l.Id)
                .ToListAsync(ct);
            if (legacyLog != null && !await _logs.Find(l => l.Id == legacyLog.Id).AnyAsync(ct))
            {
                if (attempt + 1 < attempts)
                    continue;

                legacyLog = null;
            }

            await OverlaySequenceStampsAsync(chunks, ct);
            return OrderStoredLogLines(legacyLog, chunks);
        }

        return [];
    }

    private static List<string> OrderStoredLogLines(RunLogDocument? legacyLog, List<RunLogDocument> chunks)
    {
        var orderedLines = new List<(long Sequence, int Ordinal, string Line)>();
        var existingIds = new HashSet<string>(StringComparer.Ordinal);
        if (legacyLog != null)
            existingIds.Add(legacyLog.Id);
        foreach (var chunk in chunks)
            existingIds.Add(chunk.Id);

        var fallbackSequence = 0L;
        var ordinal = 0;
        if (legacyLog != null)
        {
            foreach (var line in legacyLog.Lines)
                orderedLines.Add((fallbackSequence++, ordinal++, line));
        }

        foreach (var chunk in chunks)
        {
            if (IsPendingSplitReplacement(chunk.SplitFromId, existingIds))
                continue;

            var chunkSequences = chunk.LineSequences ?? [];
            for (var i = 0; i < chunk.Lines.Count; i++)
            {
                var sequence = NextLogSequence(i, chunkSequences, ref fallbackSequence);
                orderedLines.Add((sequence, ordinal++, chunk.Lines[i]));
            }
        }

        return orderedLines
            .OrderBy(l => l.Sequence)
            .ThenBy(l => l.Ordinal)
            .Select(l => l.Line)
            .ToList();
    }

    /// <summary>
    /// Sequence GetLogs would assign to the first unsequenced line of
    /// <paramref name="chunk"/> while that chunk is still stored. Replacements
    /// whose source is still present are ignored, matching the reader.
    /// </summary>
    private async Task<long> FallbackSequenceBeforeChunkAsync(RunLogDocument chunk, CancellationToken ct)
    {
        var legacy = await _logs.Find(l => l.Id == chunk.RunId.ToString())
            .Project(l => new RunLogDocument { Id = l.Id, LineCount = l.LineCount, Lines = l.Lines })
            .FirstOrDefaultAsync(ct);
        var prefix = CreateLogChunkPrefix(chunk.RunId);
        var earlier = await _logs.Find(Builders<RunLogDocument>.Filter.And(
                Builders<RunLogDocument>.Filter.Gte(l => l.Id, prefix),
                Builders<RunLogDocument>.Filter.Lt(l => l.Id, chunk.Id)))
            .SortBy(l => l.Id)
            .Project(l => new RunLogDocument
            {
                Id = l.Id,
                LineCount = l.LineCount,
                LineSequences = l.LineSequences,
                SplitFromId = l.SplitFromId
            })
            .ToListAsync(ct);
        await OverlaySequenceStampsAsync(earlier, ct);

        var existingIds = new HashSet<string>(StringComparer.Ordinal) { chunk.Id };
        if (legacy != null)
            existingIds.Add(legacy.Id);
        foreach (var item in earlier)
            existingIds.Add(item.Id);

        var fallback = 0L;
        if (legacy != null && !string.Equals(legacy.Id, chunk.Id, StringComparison.Ordinal))
            fallback += legacy.Lines.Count > 0 ? legacy.Lines.Count : legacy.LineCount;

        foreach (var item in earlier)
        {
            if (IsPendingSplitReplacement(item.SplitFromId, existingIds))
                continue;

            var sequences = item.LineSequences ?? [];
            var count = item.LineCount;
            for (var i = 0; i < count; i++)
                _ = NextLogSequence(i, sequences, ref fallback);
        }

        return fallback;
    }

    private async Task<bool> RewriteLegacyLogByteCountsAsync(CancellationToken ct)
    {
        var sawLegacy = false;
        string? afterId = null;
        while (!ct.IsCancellationRequested)
        {
            var filter = afterId == null
                ? Builders<RunLogDocument>.Filter.Ne(l => l.ByteCountVersion, EscapedLogByteCountVersion)
                : Builders<RunLogDocument>.Filter.And(
                    Builders<RunLogDocument>.Filter.Ne(l => l.ByteCountVersion, EscapedLogByteCountVersion),
                    Builders<RunLogDocument>.Filter.Gt(l => l.Id, afterId));
            var chunk = await _logs.Find(filter).SortBy(l => l.Id).Limit(1).FirstOrDefaultAsync(ct);
            if (chunk == null)
                return sawLegacy;

            sawLegacy = true;

            afterId = chunk.Id;
            if (!LogMetadataFits(chunk))
                continue;

            var estimate = EstimateChunkLinesBsonBytes(chunk.Lines);
            await CosmosThrottle.ExecuteAsync(
                token => _logs.UpdateOneAsync(
                    Builders<RunLogDocument>.Filter.And(
                        Builders<RunLogDocument>.Filter.Eq(l => l.Id, chunk.Id),
                        Builders<RunLogDocument>.Filter.Eq(l => l.LineCount, chunk.LineCount),
                        Builders<RunLogDocument>.Filter.Ne(l => l.ByteCountVersion, EscapedLogByteCountVersion)),
                    Builders<RunLogDocument>.Update
                        .Set(l => l.BsonByteCount, estimate)
                        .Set(l => l.ByteCountVersion, EscapedLogByteCountVersion),
                    cancellationToken: token),
                ct,
                _logger);
        }

        return sawLegacy;
    }

    private static List<long> EffectiveLogSequences(RunLogDocument chunk, long fallback)
    {
        var stored = chunk.LineSequences ?? [];
        var effective = new List<long>(chunk.Lines.Count);
        for (var i = 0; i < chunk.Lines.Count; i++)
            effective.Add(NextLogSequence(i, stored, ref fallback));
        return effective;
    }

    private async Task DeleteAbandonedSplitAsync(RunLogDocument chunk, int start, int count, CancellationToken ct)
    {
        // The source can be gone because another worker already published the
        // split and has not cleared SplitFromId yet. Those replacements are the
        // only copy of the lines. Remove them only when the run itself is gone.
        var runExists = await _runs.Find(r => r.RunId == chunk.RunId).AnyAsync(ct);
        if (!runExists)
        {
            await CosmosThrottle.ExecuteAsync(
                token => _logs.DeleteManyAsync(
                    l => l.SplitFromId == chunk.Id
                        && l.ChunkNumber >= start
                        && l.ChunkNumber < start + count,
                    token),
                ct,
                _logger);
            await CosmosThrottle.ExecuteAsync(
                token => _logSequenceStamps.DeleteOneAsync(s => s.Id == chunk.Id, token),
                ct,
                _logger);
            await CosmosThrottle.ExecuteAsync(
                token => _logSplitClaims.DeleteOneAsync(
                    c => c.Id == chunk.Id && c.Owner == _logSplitOwner,
                    token),
                ct,
                _logger);
        }

        if (AfterAbandonedSplitConsideredForTests != null)
        {
            var considered = AfterAbandonedSplitConsideredForTests;
            AfterAbandonedSplitConsideredForTests = null;
            await considered(ct);
        }
    }

    private static bool IsPendingSplitReplacement(string? splitFromId, IReadOnlySet<string> existingIds)
        => !string.IsNullOrEmpty(splitFromId) && existingIds.Contains(splitFromId);

    private static long NextLogSequence(int index, IReadOnlyList<long> sequences, ref long fallback)
    {
        if (index < sequences.Count)
        {
            var sequence = sequences[index];
            if (fallback <= sequence)
                fallback = sequence + 1;
            return sequence;
        }

        return fallback++;
    }

    private async Task StampFollowingUnsequencedChunksAsync(RunLogDocument chunk, long firstSequence, CancellationToken ct)
    {
        var next = firstSequence;
        var sourceSequences = chunk.LineSequences ?? [];
        for (var i = 0; i < chunk.Lines.Count; i++)
            NextLogSequence(i, sourceSequences, ref next);
        var later = await _logs.Find(Builders<RunLogDocument>.Filter.And(
                CreateLogChunkFilter(chunk.RunId),
                Builders<RunLogDocument>.Filter.Gt(l => l.Id, chunk.Id)))
            .SortBy(l => l.Id)
            .Project(l => new RunLogDocument
            {
                Id = l.Id,
                LineCount = l.LineCount,
                LineSequences = l.LineSequences,
                SplitFromId = l.SplitFromId
            })
            .ToListAsync(ct);

        var existingIds = new HashSet<string>(StringComparer.Ordinal) { chunk.Id };
        foreach (var item in later)
            existingIds.Add(item.Id);

        // A replacement of an earlier chunk is past this id, so the source is
        // not in the page above. It is still hidden while that source exists.
        var unknownSources = new List<string>();
        foreach (var item in later)
        {
            if (string.IsNullOrEmpty(item.SplitFromId) || existingIds.Contains(item.SplitFromId))
                continue;
            if (!unknownSources.Contains(item.SplitFromId))
                unknownSources.Add(item.SplitFromId);
        }

        if (unknownSources.Count > 0)
        {
            var present = await _logs.Find(Builders<RunLogDocument>.Filter.In(l => l.Id, unknownSources))
                .Project(l => l.Id)
                .ToListAsync(ct);
            foreach (var id in present)
                existingIds.Add(id);
        }

        foreach (var item in later)
        {
            if (item.LineCount == 0 || IsPendingSplitReplacement(item.SplitFromId, existingIds))
                continue;

            var projectedSequences = item.LineSequences ?? [];
            if (projectedSequences.Count == item.LineCount)
            {
                var max = projectedSequences.Max();
                if (next <= max)
                    next = max + 1;
                continue;
            }

            var full = await _logs.Find(l => l.Id == item.Id).FirstOrDefaultAsync(ct);
            if (full == null || full.LineCount != item.LineCount)
                continue;

            await OverlaySequenceStampsAsync([full], ct);
            var sequences = full.LineSequences ?? [];
            if (sequences.Count == full.LineCount)
            {
                var max = sequences.Max();
                if (next <= max)
                    next = max + 1;
                continue;
            }

            var assigned = new List<long>(full.LineCount);
            for (var i = 0; i < full.LineCount; i++)
                assigned.Add(NextLogSequence(i, sequences, ref next));

            if (LineSequencesFit(full, assigned))
            {
                await CosmosThrottle.ExecuteAsync(
                    token => _logs.UpdateOneAsync(
                        l => l.Id == full.Id && l.LineCount == full.LineCount,
                        Builders<RunLogDocument>.Update.Set(l => l.LineSequences, assigned),
                        cancellationToken: token),
                    ct,
                    _logger);
                continue;
            }

            // The chunk is already at the document cap. Sequences live beside it.
            await CosmosThrottle.ExecuteAsync(
                token => _logSequenceStamps.ReplaceOneAsync(
                    s => s.Id == full.Id,
                    new LogSequenceStampDocument { Id = full.Id, LineSequences = assigned },
                    new ReplaceOptions { IsUpsert = true },
                    token),
                ct,
                _logger);
        }
    }

    private async Task OverlaySequenceStampsAsync(IReadOnlyList<RunLogDocument> chunks, CancellationToken ct)
    {
        var pending = new List<RunLogDocument>();
        foreach (var chunk in chunks)
        {
            if (chunk.LineCount > 0 && (chunk.LineSequences?.Count ?? 0) != chunk.LineCount)
                pending.Add(chunk);
        }

        if (pending.Count == 0)
            return;

        var ids = pending.Select(chunk => chunk.Id).ToList();
        var stamps = await _logSequenceStamps
            .Find(Builders<LogSequenceStampDocument>.Filter.In(s => s.Id, ids))
            .ToListAsync(ct);
        var byId = stamps.ToDictionary(stamp => stamp.Id, StringComparer.Ordinal);
        foreach (var chunk in pending)
        {
            if (byId.TryGetValue(chunk.Id, out var stamp) && stamp.LineSequences.Count == chunk.LineCount)
                chunk.LineSequences = stamp.LineSequences;
        }
    }

    /// <summary>
    /// True when the chunk document can store <paramref name="sequences"/> and
    /// stay under the Cosmos 2 MB cap. The caller's document is not modified.
    /// </summary>
    private static bool LineSequencesFit(RunLogDocument chunk, IReadOnlyList<long> sequences)
    {
        var measured = new RunLogDocument
        {
            Id = chunk.Id,
            RunId = chunk.RunId,
            ChunkNumber = chunk.ChunkNumber,
            LineCount = chunk.LineCount,
            BsonByteCount = chunk.BsonByteCount,
            Lines = chunk.Lines,
            LineSequences = [.. sequences],
            UpdatedAt = chunk.UpdatedAt,
            SplitStart = chunk.SplitStart,
            SplitFromId = chunk.SplitFromId,
            SplitCount = chunk.SplitCount,
            SplitOwner = chunk.SplitOwner,
            SplitAttempt = chunk.SplitAttempt,
            SplitClaimedAt = chunk.SplitClaimedAt,
            ByteCountVersion = chunk.ByteCountVersion
        };
        var bytes = Encoding.UTF8.GetByteCount(measured.ToBsonDocument().ToJson());
        return bytes <= SnapshotPartitioner.HardCapBytes;
    }

    private async Task<RunLogDocument?> FindAppendTargetAsync(Guid runId, CancellationToken ct)
    {
        // The newest chunk is the only append target. Walking back to an older
        // chunk while this one is frozen puts the new line ahead of unsequenced
        // lines that GetLogs still reads from the frozen source.
        var newest = await _logs.Find(CreateLogChunkFilter(runId))
            .SortByDescending(l => l.Id)
            .Limit(1)
            .Project(l => new RunLogDocument
            {
                Id = l.Id,
                SplitFromId = l.SplitFromId,
                SplitStart = l.SplitStart
            })
            .FirstOrDefaultAsync(ct);
        if (newest == null || await IsFrozenForAppendAsync(newest, ct))
            return null;

        if (!string.IsNullOrEmpty(newest.SplitFromId))
        {
            await CosmosThrottle.ExecuteAsync(
                token => _logs.UpdateOneAsync(
                    l => l.Id == newest.Id && l.SplitFromId == newest.SplitFromId,
                    Builders<RunLogDocument>.Update.Unset(l => l.SplitFromId),
                    cancellationToken: token),
                ct,
                _logger);
        }

        return await _logs.Find(l => l.Id == newest.Id).FirstOrDefaultAsync(ct);
    }

    private async Task<bool> IsFrozenForAppendAsync(RunLogDocument chunk, CancellationToken ct)
    {
        if (chunk.SplitStart != null)
            return true;

        if (string.IsNullOrEmpty(chunk.SplitFromId))
            return false;

        return await _logs.Find(l => l.Id == chunk.SplitFromId).AnyAsync(ct);
    }

    private async Task<int> NextAppendChunkNumberAsync(Guid runId, CancellationToken ct)
    {
        // Same _id order as the split claim. ChunkNumber is not indexed, and
        // Cosmos DB for MongoDB RU rejects that sort.
        var highest = await _logs.Find(CreateLogChunkFilter(runId))
            .SortByDescending(l => l.Id)
            .Project(l => (int?)l.ChunkNumber)
            .FirstOrDefaultAsync(ct);
        var next = (highest ?? -1) + 1;
        var reserved = await _logs.Find(Builders<RunLogDocument>.Filter.And(
                CreateLogChunkFilter(runId),
                Builders<RunLogDocument>.Filter.Ne(l => l.SplitStart, null)))
            .Project(l => new RunLogDocument { SplitStart = l.SplitStart, SplitCount = l.SplitCount })
            .ToListAsync(ct);
        foreach (var item in reserved)
        {
            if (item.SplitStart == null)
                continue;

            var end = item.SplitStart.Value + Math.Max(item.SplitCount ?? 1, 1);
            if (next < end)
                next = end;
        }

        // A near-cap source stores its reservation beside the chunk. Append has
        // to stay past that range or it lands on a replacement id.
        var externalClaims = await _logSplitClaims.Find(CreateLogSplitClaimFilter(runId))
            .Project(c => new LogSplitClaimDocument { SplitStart = c.SplitStart, SplitCount = c.SplitCount })
            .ToListAsync(ct);
        foreach (var claim in externalClaims)
        {
            var end = claim.SplitStart + Math.Max(claim.SplitCount, 1);
            if (next < end)
                next = end;
        }

        return next;
    }

    private static FilterDefinition<RunLogDocument> CreateLogChunkFilter(Guid runId)
    {
        var prefix = CreateLogChunkPrefix(runId);
        return Builders<RunLogDocument>.Filter.And(
            Builders<RunLogDocument>.Filter.Gte(l => l.Id, prefix),
            Builders<RunLogDocument>.Filter.Lt(l => l.Id, prefix + '\uffff'));
    }

    private static FilterDefinition<LogSplitClaimDocument> CreateLogSplitClaimFilter(Guid runId)
    {
        // A near-cap legacy chunk is stored under runId.ToString(), which is the
        // hyphenated form. Its lease uses that same id and is outside the compact prefix.
        var prefix = CreateLogChunkPrefix(runId);
        return Builders<LogSplitClaimDocument>.Filter.Or(
            Builders<LogSplitClaimDocument>.Filter.And(
                Builders<LogSplitClaimDocument>.Filter.Gte(c => c.Id, prefix),
                Builders<LogSplitClaimDocument>.Filter.Lt(c => c.Id, prefix + '\uffff')),
            Builders<LogSplitClaimDocument>.Filter.Eq(c => c.Id, runId.ToString()));
    }

    private static FilterDefinition<LogSequenceStampDocument> CreateSequenceStampFilter(Guid runId)
    {
        var prefix = CreateLogChunkPrefix(runId);
        return Builders<LogSequenceStampDocument>.Filter.Or(
            Builders<LogSequenceStampDocument>.Filter.And(
                Builders<LogSequenceStampDocument>.Filter.Gte(s => s.Id, prefix),
                Builders<LogSequenceStampDocument>.Filter.Lt(s => s.Id, prefix + '\uffff')),
            Builders<LogSequenceStampDocument>.Filter.Eq(s => s.Id, runId.ToString()));
    }

    private static string CreateLogChunkId(Guid runId, int chunkNumber) => $"{CreateLogChunkPrefix(runId)}{chunkNumber:D8}";

    private static string CreateLogChunkPrefix(Guid runId) => $"{runId:N}:";
}
