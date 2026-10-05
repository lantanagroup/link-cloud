using LantanaGroup.Link.Automation.Link.Helpers;
using MongoDB.Bson;
using MongoDB.Driver;
using LantanaGroup.Link.Shared.Application.Services.Security;
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
///   automation_runs            — lightweight run metadata
///   automation_snapshots       — per-run, per-domain polling data (upsert on RunId+Domain)
///   automation_snapshot_clocks — writer clock per run and domain, kept off the snapshot document
///   automation_logs            — full log output per run
///
/// Indexes are managed centrally by <see cref="MongoIndexManager"/>.
/// </summary>
public sealed class MongoSnapshotStore : ISnapshotStore
{
    private const int MaxLogLinesPerChunk = 1_000;

    /// <summary>
    /// Cosmos DB for MongoDB RU rejects a document over 2 MB. Snapshot slices
    /// and run-log chunks stay at or under 1.5 MB so the wrapper still fits.
    /// </summary>
    internal const int SnapshotChunkBytes = 1_500_000;

    private const int MaxLogChunkEstimatedBsonBytes = SnapshotChunkBytes;
    private const int EstimatedBsonBytesPerLineOverhead = 64;
    private const string OversizedLogLineSuffix = " [truncated: exceeded log chunk byte budget]";
    private const string SnapshotPayloadPointerEnvelopeProperty = "__externalSnapshotPayloadPointer";

    private readonly IMongoCollection<AutomationRunDocument> _runs;
    private readonly IMongoCollection<AutomationRunInputDocument> _runInputs;
    private readonly IMongoCollection<DomainSnapshotDocument> _snapshots;
    private readonly IMongoCollection<SnapshotWriteClockDocument> _writeClocks;
    private readonly IMongoCollection<RunLogDocument> _logs;
    private readonly IMongoCollection<RunLogSequenceDocument> _logSequences;
    private readonly IMongoCollection<ImportedBundleDocument> _importedBundles;
    private readonly IMongoCollection<OwnedFacilityTombstoneDocument> _ownedFacilityTombstones;
    private readonly IMongoCollection<FacilityTeardownProgressDocument> _facilityTeardownProgress;
    private readonly ISnapshotPayloadStore _snapshotPayloadStore;
    private readonly ILogger<MongoSnapshotStore> _logger;

    /// <summary>
    /// Test seam. Runs after a single-document header update has been sent
    /// and before this write treats that update as acknowledged.
    /// </summary>
    internal Func<Task>? AfterSingleHeaderUpdate { get; set; }

    /// <summary>
    /// Test seam. Runs after a chunked header flip has been sent and before
    /// this write treats that flip as acknowledged.
    /// </summary>
    internal Func<Task>? AfterChunkedHeaderUpdate { get; set; }

    /// <summary>
    /// Test seam. Runs after this write's header is stored and before the
    /// displaced-header lookup.
    /// </summary>
    internal Func<Task>? BeforeDeleteDisplaced { get; set; }

    /// <summary>Test seam. Runs before this write stores a header.</summary>
    internal Func<Task>? BeforeHeaderWrite { get; set; }

    /// <summary>Test seam. Replaces <see cref="DateTimeOffset.UtcNow"/> for one store.</summary>
    internal Func<DateTimeOffset>? Clock { get; set; }

    /// <summary>Test seam. The next header lookup throws once.</summary>
    internal bool FailNextHeaderLookup { get; set; }

    /// <summary>
    /// Test seam. Runs after a lost acknowledgement has been confirmed as
    /// published and before the follow-up header lookup.
    /// </summary>
    internal Func<Task>? AfterPublishConfirmed { get; set; }

    public MongoSnapshotStore(IMongoDatabase database, ILogger<MongoSnapshotStore> logger, ISnapshotPayloadStore? snapshotPayloadStore = null)
    {
        _runs = database.GetCollection<AutomationRunDocument>("automation_runs");
        _runInputs = database.GetCollection<AutomationRunInputDocument>("automation_run_inputs");
        _snapshots = database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        _writeClocks = database.GetCollection<SnapshotWriteClockDocument>("automation_snapshot_clocks");
        _logs = database.GetCollection<RunLogDocument>("automation_logs");
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
        await _writeClocks.DeleteManyAsync(c => c.RunId == runId, ct);
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
        await _writeClocks.DeleteManyAsync(c => c.RunId == runId, ct);
        await _logs.DeleteManyAsync(CreateLogChunkFilter(runId), ct);
        await _logs.DeleteOneAsync(l => l.Id == runId.ToString(), ct);

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
        var payloadUtf8Bytes = Encoding.UTF8.GetByteCount(json);
        var now = Clock?.Invoke() ?? DateTimeOffset.UtcNow;

        SnapshotPayloadPointer? newPointer = null;
        var storedJson = json;
        if (_snapshotPayloadStore.ShouldExternalize(domain, payloadUtf8Bytes))
        {
            newPointer = await _snapshotPayloadStore.StoreAsync(runId, domain, json, ct);
            storedJson = JsonSerializer.Serialize(new Dictionary<string, SnapshotPayloadPointer?>
            {
                [SnapshotPayloadPointerEnvelopeProperty] = newPointer
            });
        }

        var storedBytes = Encoding.UTF8.GetByteCount(storedJson);
        SnapshotWrite write;
        try
        {
            write = storedBytes <= SnapshotChunkBytes
                ? await WriteSingleAsync(runId, domain, storedJson, now, ct)
                : await WriteChunkedAsync(runId, domain, storedJson, now, ct);
        }
        catch
        {
            if (newPointer != null)
                await DeleteDisplacedBlobIfUnreferencedAsync(runId, domain, newPointer);
            throw;
        }

        if (!write.Wrote)
        {
            if (newPointer != null)
                await DeleteUnusedSnapshotBlobAsync(runId, domain, newPointer);
            if (write.Displaced != null && (newPointer == null || !string.Equals(write.Displaced.BlobName, newPointer.BlobName, StringComparison.Ordinal)))
                await DeleteDisplacedBlobIfUnreferencedAsync(runId, domain, write.Displaced);
            return;
        }

        if (write.Displaced != null && (newPointer == null || !string.Equals(write.Displaced.BlobName, newPointer.BlobName, StringComparison.Ordinal)))
            await DeleteUnusedSnapshotBlobAsync(runId, domain, write.Displaced);
    }

    public async Task<DomainSnapshot<T>?> GetDomainAsync<T>(Guid runId, string domain, CancellationToken ct = default)
    {
        var filter = Builders<DomainSnapshotDocument>.Filter.Eq(d => d.RunId, runId)
            & Builders<DomainSnapshotDocument>.Filter.Eq(d => d.Domain, domain);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var docs = await _snapshots.Find(filter).ToListAsync(ct);
            if (docs.Count == 0)
            {
                _logger.LogDebug("[Store] GetDomain: no document for run={RunId} domain={Domain}", runId, domain);
                return null;
            }

            var chosen = docs.Where(d => d.ChunkIndex is null or -1)
                .OrderByDescending(d => d.UpdatedAt)
                .ThenByDescending(d => d.Id)
                .FirstOrDefault();
            string payloadJson;
            DateTimeOffset updatedAt;
            if (chosen?.ChunkIndex == -1 && chosen.ChunkCount is > 0 && !string.IsNullOrEmpty(chosen.Revision))
            {
                var chunks = docs
                    .Where(d => d.ChunkIndex >= 0 && d.Revision == chosen.Revision)
                    .OrderBy(d => d.ChunkIndex)
                    .ToList();
                var expected = chosen.ChunkCount.Value;
                var complete = chunks.Count == expected
                    && chunks.Select((chunk, index) => chunk.ChunkIndex == index).All(match => match);
                if (!complete)
                {
                    if (attempt < 2)
                        continue;

                    _logger.LogWarning(
                        "[Store] GetDomain: incomplete chunks for run={RunId} domain={Domain} revision={Revision} expected={Expected} found={Found}",
                        runId, domain, chosen.Revision, chosen.ChunkCount, chunks.Count);
                    return null;
                }

                payloadJson = string.Concat(chunks.Select(c => c.Data));
                updatedAt = chosen.UpdatedAt;
            }
            else
            {
                if (chosen == null || string.IsNullOrEmpty(chosen.Data))
                    return null;

                payloadJson = chosen.Data;
                updatedAt = chosen.UpdatedAt;
            }

            try
            {
                var pointer = TryReadSnapshotPayloadPointer(payloadJson);
                if (pointer != null)
                {
                    payloadJson = await _snapshotPayloadStore.ReadAsync(pointer, ct) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(payloadJson))
                    {
                        var sanitizedRunId = runId.ToString().SanitizeForLog();
                        var sanitizedDomain = domain.SanitizeForLog();
                        var sanitizedBlobName = pointer.BlobName.SanitizeForLog();
                        _logger.LogWarning("[Store] GetDomain: externalized payload missing for run={RunId} domain={Domain} blob={Blob}", sanitizedRunId, sanitizedDomain, sanitizedBlobName);
                        return null;
                    }
                }

                var data = JsonSerializer.Deserialize<T>(payloadJson);
                if (data == null)
                {
                    _logger.LogDebug("[Store] GetDomain: deserialized to null for run={RunId} domain={Domain} (json length={Len})", runId, domain, payloadJson.Length);
                    return null;
                }

                return new DomainSnapshot<T> { UpdatedAt = updatedAt, Data = data };
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "[Store] GetDomain: deserialization failed for run={RunId} domain={Domain} type={Type} (json length={Len})", runId, domain, typeof(T).Name, payloadJson.Length);
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Splits <paramref name="json"/> on UTF-8 character boundaries so each
    /// slice is at most <paramref name="maxBytes"/> bytes. A slice is not
    /// valid JSON on its own. Concatenate the slices before deserializing.
    /// </summary>
    internal static List<string> SplitUtf8(string json, int maxBytes)
    {
        var slices = new List<string>();
        if (string.IsNullOrEmpty(json))
            return slices;

        var start = 0;
        var bytes = 0;
        for (var i = 0; i < json.Length;)
        {
            var width = char.IsHighSurrogate(json[i]) && i + 1 < json.Length ? 2 : 1;
            var charBytes = Encoding.UTF8.GetByteCount(json.AsSpan(i, width));
            if (bytes > 0 && bytes + charBytes > maxBytes)
            {
                slices.Add(json.Substring(start, i - start));
                start = i;
                bytes = 0;
            }

            bytes += charBytes;
            i += width;
        }

        if (start < json.Length)
            slices.Add(json[start..]);

        return slices;
    }

    private FilterDefinition<DomainSnapshotDocument> SingleOrHeaderFilter(Guid runId, string domain)
    {
        var filter = Builders<DomainSnapshotDocument>.Filter;
        return filter.Eq(d => d.RunId, runId)
            & filter.Eq(d => d.Domain, domain)
            & (filter.Eq(d => d.ChunkIndex, null) | filter.Eq(d => d.ChunkIndex, -1));
    }

    private readonly record struct SnapshotWrite(bool Wrote, SnapshotPayloadPointer? Displaced);

    /// <summary>
    /// BSON datetimes keep milliseconds. A stored header must be strictly newer
    /// than the one this write observed, or a same-millisecond update still matches
    /// <see cref="ObservedHeaderFilter"/>.
    /// </summary>
    internal static DateTimeOffset NextSnapshotTimestamp(DateTimeOffset now, DateTimeOffset? previous)
    {
        if (previous == null || now > previous.Value)
            return now;

        return previous.Value.AddMilliseconds(1);
    }

    /// <summary>
    /// True when the stored header was written by a clock later than this write.
    /// The clock lives in <c>automation_snapshot_clocks</c>. Older rows have no clock
    /// document, so <see cref="DomainSnapshotDocument.UpdatedAt"/> is the fallback.
    /// That timestamp also moves forward to keep compare-and-swap unique.
    /// </summary>
    internal static bool StoredHeaderIsNewerThan(DomainSnapshotDocument previous, DateTimeOffset now, DateTimeOffset? writeClock)
    {
        var clock = writeClock ?? previous.UpdatedAt;
        return clock > now;
    }

    internal static string SnapshotClockId(Guid runId, string domain) => $"{runId:N}|{domain}";

    private async Task<DateTimeOffset?> ReadWriteClockAsync(Guid runId, string domain, CancellationToken ct)
    {
        var doc = await _writeClocks.Find(c => c.Id == SnapshotClockId(runId, domain)).FirstOrDefaultAsync(ct);
        return doc == null ? null : doc.WriteClock;
    }

    private Task RememberWriteClockAsync(Guid runId, string domain, DateTimeOffset now, CancellationToken ct)
    {
        var id = SnapshotClockId(runId, domain);
        return _writeClocks.ReplaceOneAsync(
            c => c.Id == id,
            new SnapshotWriteClockDocument
            {
                Id = id,
                RunId = runId,
                Domain = domain,
                WriteClock = now
            },
            new ReplaceOptions { IsUpsert = true },
            ct);
    }

    private async Task<SnapshotWrite> WriteSingleAsync(Guid runId, string domain, string storedJson, DateTimeOffset now, CancellationToken ct)
    {
        if (BeforeHeaderWrite != null)
            await BeforeHeaderWrite();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            var previous = await FindNewestHeaderAsync(runId, domain, ct);
            var writeClock = previous == null ? null : await ReadWriteClockAsync(runId, domain, ct);
            if (previous != null && StoredHeaderIsNewerThan(previous, now, writeClock))
            {
                LogDroppedNewerHeader(runId, domain);
                return new SnapshotWrite(false, null);
            }

            var previousRevision = previous is { ChunkIndex: -1 } ? previous.Revision : null;
            var stamp = NextSnapshotTimestamp(now, previous?.UpdatedAt);
            ObjectId keepId;
            if (previous == null)
            {
                var created = new DomainSnapshotDocument
                {
                    Id = ObjectId.GenerateNewId(),
                    RunId = runId,
                    Domain = domain,
                    Data = storedJson,
                    UpdatedAt = stamp
                };
                try
                {
                    await _snapshots.InsertOneAsync(created, cancellationToken: ct);
                }
                catch (Exception)
                {
                    if (!await DocumentExistsAsync(created.Id))
                        throw;
                }

                keepId = created.Id;
            }
            else
            {
                var update = Builders<DomainSnapshotDocument>.Update
                    .Set(d => d.Data, storedJson)
                    .Set(d => d.UpdatedAt, stamp)
                    .Unset(d => d.ChunkIndex)
                    .Unset(d => d.ChunkCount)
                    .Unset(d => d.Revision);
                try
                {
                    var result = await _snapshots.UpdateOneAsync(ObservedHeaderFilter(previous), update, cancellationToken: ct);
                    if (result.MatchedCount == 0)
                        continue;

                    keepId = previous.Id;
                    if (AfterSingleHeaderUpdate != null)
                        await AfterSingleHeaderUpdate();
                }
                catch (Exception)
                {
                    // The update may have landed even though the acknowledgement did not.
                    // The header no longer carries the old revision or the old blob pointer,
                    // so a later write cannot find them. Drop each only when nothing still names it.
                    await ReclaimUnreferencedRevisionAsync(runId, domain, previousRevision);
                    await ReclaimDisplacedBlobAsync(runId, domain, previous);
                    throw;
                }
            }

            if (!await DeleteDisplacedAsync(runId, domain, keepId, previousRevision, previous))
            {
                _logger.LogWarning(
                    "Snapshot domain {Domain} for run {RunId} dropped this write because a newer snapshot header won.",
                    domain.SanitizeForLog(),
                    runId.ToString().SanitizeForLog());
                return new SnapshotWrite(false, DisplacedPointer(previous));
            }

            await RememberWriteClockAsync(runId, domain, now, ct);
            return new SnapshotWrite(true, DisplacedPointer(previous));
        }

        _logger.LogWarning(
            "Snapshot domain {Domain} for run {RunId} kept its previous document after a concurrent write.",
            domain.SanitizeForLog(),
            runId.ToString().SanitizeForLog());
        return new SnapshotWrite(false, null);
    }

    private async Task<SnapshotWrite> WriteChunkedAsync(Guid runId, string domain, string json, DateTimeOffset now, CancellationToken ct)
    {
        if (BeforeHeaderWrite != null)
            await BeforeHeaderWrite();

        return await WriteChunkedCoreAsync(runId, domain, json, now, ct);
    }

    private async Task<SnapshotWrite> WriteChunkedCoreAsync(Guid runId, string domain, string json, DateTimeOffset now, CancellationToken ct)
    {
        var slices = SplitUtf8(json, SnapshotChunkBytes);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            var previous = await FindNewestHeaderAsync(runId, domain, ct);
            var writeClock = previous == null ? null : await ReadWriteClockAsync(runId, domain, ct);
            if (previous != null && StoredHeaderIsNewerThan(previous, now, writeClock))
            {
                LogDroppedNewerHeader(runId, domain);
                return new SnapshotWrite(false, null);
            }

            var previousRevision = previous is { ChunkIndex: -1 } ? previous.Revision : null;
            var stamp = NextSnapshotTimestamp(now, previous?.UpdatedAt);
            var revision = Guid.NewGuid().ToString("N");

            try
            {
                for (var i = 0; i < slices.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    await _snapshots.InsertOneAsync(new DomainSnapshotDocument
                    {
                        Id = ObjectId.GenerateNewId(),
                        RunId = runId,
                        Domain = domain,
                        Data = slices[i],
                        ChunkIndex = i,
                        ChunkCount = slices.Count,
                        Revision = revision,
                        UpdatedAt = now
                    }, cancellationToken: ct);
                }
            }
            catch (Exception)
            {
                if (await HeaderHasRevisionAsync(runId, domain, revision) == false)
                    await DeleteSlicesQuietlyAsync(runId, domain, revision);

                throw;
            }

            ObjectId keepId;
            try
            {
                if (previous == null)
                {
                    var created = new DomainSnapshotDocument
                    {
                        Id = ObjectId.GenerateNewId(),
                        RunId = runId,
                        Domain = domain,
                        Data = string.Empty,
                        ChunkIndex = -1,
                        ChunkCount = slices.Count,
                        Revision = revision,
                        UpdatedAt = stamp
                    };
                    await _snapshots.InsertOneAsync(created, cancellationToken: ct);
                    keepId = created.Id;
                }
                else
                {
                    var headerUpdate = Builders<DomainSnapshotDocument>.Update
                        .Set(d => d.Data, string.Empty)
                        .Set(d => d.ChunkIndex, -1)
                        .Set(d => d.ChunkCount, slices.Count)
                        .Set(d => d.Revision, revision)
                        .Set(d => d.UpdatedAt, stamp);
                    var flip = await _snapshots.UpdateOneAsync(ObservedHeaderFilter(previous), headerUpdate, cancellationToken: ct);
                    if (flip.MatchedCount == 0)
                    {
                        await DeleteSlicesAsync(runId, domain, revision, ct);
                        continue;
                    }

                    keepId = previous.Id;
                    if (AfterChunkedHeaderUpdate != null)
                        await AfterChunkedHeaderUpdate();
                }
            }
            catch (Exception)
            {
                var published = await HeaderHasRevisionAsync(runId, domain, revision);
                if (published != true)
                {
                    if (published == false)
                        await DeleteSlicesQuietlyAsync(runId, domain, revision);

                    await ReclaimUnreferencedRevisionAsync(runId, domain, previousRevision);
                    await ReclaimDisplacedBlobAsync(runId, domain, previous);
                    throw;
                }

                if (AfterPublishConfirmed != null)
                    await AfterPublishConfirmed();

                DomainSnapshotDocument? publishedHeader;
                try
                {
                    using var timeout = StartCleanupLookupTimeout();
                    publishedHeader = (await FindHeadersAsync(runId, domain, timeout.Token))
                        .FirstOrDefault(header => header.ChunkIndex == -1 && header.Revision == revision);
                }
                catch (Exception lookupEx)
                {
                    _logger.LogWarning(
                        lookupEx,
                        "Snapshot header lookup failed for domain {Domain} run {RunId}.",
                        domain.SanitizeForLog(),
                        runId.ToString().SanitizeForLog());
                    publishedHeader = null;
                }
                if (publishedHeader == null)
                {
                    await ReclaimUnreferencedRevisionAsync(runId, domain, previousRevision);
                    await ReclaimUnreferencedRevisionAsync(runId, domain, revision);
                    await ReclaimDisplacedBlobAsync(runId, domain, previous);
                    throw;
                }

                keepId = publishedHeader.Id;
            }

            if (!await DeleteDisplacedAsync(runId, domain, keepId, previousRevision, previous))
            {
                _logger.LogWarning(
                    "Snapshot domain {Domain} for run {RunId} dropped this write because a newer snapshot header won.",
                    domain.SanitizeForLog(),
                    runId.ToString().SanitizeForLog());
                return new SnapshotWrite(false, DisplacedPointer(previous));
            }

            await RememberWriteClockAsync(runId, domain, now, ct);
            return new SnapshotWrite(true, DisplacedPointer(previous));
        }

        _logger.LogWarning(
            "Snapshot domain {Domain} for run {RunId} kept its previous revision after a concurrent write.",
            domain.SanitizeForLog(),
            runId.ToString().SanitizeForLog());
        return new SnapshotWrite(false, null);
    }

    private async Task<DomainSnapshotDocument?> FindNewestHeaderAsync(Guid runId, string domain, CancellationToken ct)
    {
        var headers = await FindHeadersAsync(runId, domain, ct);
        return headers.OrderByDescending(header => header.UpdatedAt).ThenByDescending(header => header.Id).FirstOrDefault();
    }

    private Task<List<DomainSnapshotDocument>> FindHeadersAsync(Guid runId, string domain, CancellationToken ct)
    {
        if (FailNextHeaderLookup)
        {
            FailNextHeaderLookup = false;
            throw new IOException("lookup failed");
        }

        return _snapshots.Find(SingleOrHeaderFilter(runId, domain)).ToListAsync(ct);
    }

    private async Task<bool> DocumentExistsAsync(ObjectId id)
    {
        try
        {
            using var timeout = StartCleanupLookupTimeout();
            var found = await _snapshots.Find(d => d.Id == id).Limit(1).FirstOrDefaultAsync(timeout.Token);
            return found != null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Snapshot header lookup by id failed.");
            return false;
        }
    }

    /// <summary>
    /// True when a header for <paramref name="revision"/> is stored, false when it is not,
    /// and null when the lookup itself failed. A null result must not delete that revision.
    /// </summary>
    private async Task<bool?> HeaderHasRevisionAsync(Guid runId, string domain, string revision)
    {
        try
        {
            using var timeout = StartCleanupLookupTimeout();
            var headers = await FindHeadersAsync(runId, domain, timeout.Token);
            return headers.Any(header => header.ChunkIndex == -1 && header.Revision == revision);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Snapshot header lookup failed for domain {Domain} run {RunId}.",
                domain.SanitizeForLog(),
                runId.ToString().SanitizeForLog());
            return null;
        }
    }

    /// <summary>
    /// Drops headers this write displaced. Returns false when a newer header
    /// already won, in which case this write's own header is removed and its
    /// slices go with it. Two first inserts can pass each other: the newer one
    /// may finish before the older header exists, so the older write has to
    /// drop itself when it finally sees that newer header. A revision this
    /// write already replaced is dropped too, once no header still names it.
    /// </summary>
    private async Task<bool> DeleteDisplacedAsync(
        Guid runId,
        string domain,
        ObjectId keepId,
        string? previousRevision,
        DomainSnapshotDocument? previous)
    {
        if (BeforeDeleteDisplaced != null)
            await BeforeDeleteDisplaced();

        using var timeout = StartCleanupLookupTimeout();
        var ct = timeout.Token;
        List<DomainSnapshotDocument> headers;
        try
        {
            headers = await FindHeadersAsync(runId, domain, ct);
        }
        catch (Exception)
        {
            await ReclaimUnreferencedRevisionAsync(runId, domain, previousRevision);
            await ReclaimDisplacedBlobAsync(runId, domain, previous);
            throw;
        }
        var me = headers.FirstOrDefault(header => header.Id == keepId);
        if (me == null)
        {
            await ReclaimUnreferencedRevisionAsync(runId, domain, previousRevision);
            return false;
        }

        var newest = headers
            .OrderByDescending(header => header.UpdatedAt)
            .ThenByDescending(header => header.Id)
            .First();
        if (newest.Id != keepId)
        {
            await DeleteHeaderIfUnchangedAsync(runId, domain, me, ct);
            await ReclaimUnreferencedRevisionAsync(runId, domain, previousRevision);
            return false;
        }

        foreach (var loser in headers.Where(header => header.Id != keepId && LosesTo(header, keepId, me.UpdatedAt)))
            await DeleteHeaderIfUnchangedAsync(runId, domain, loser, ct);

        if (!string.IsNullOrEmpty(previousRevision))
            await DeleteSlicesAsync(runId, domain, previousRevision, ct);

        return true;
    }

    private async Task DeleteHeaderIfUnchangedAsync(
        Guid runId,
        string domain,
        DomainSnapshotDocument header,
        CancellationToken ct)
    {
        var removed = await _snapshots.DeleteOneAsync(ObservedHeaderFilter(header), ct);
        if (removed.DeletedCount == 0)
            return;

        var pointer = TryReadSnapshotPayloadPointer(header.Data);
        if (pointer != null)
            await DeleteUnusedSnapshotBlobAsync(runId, domain, pointer);

        if (string.IsNullOrEmpty(header.Revision))
            return;

        await DeleteSlicesAsync(runId, domain, header.Revision, ct);
    }

    internal static FilterDefinition<DomainSnapshotDocument> ObservedHeaderFilter(DomainSnapshotDocument previous)
    {
        var filter = Builders<DomainSnapshotDocument>.Filter;
        var revision = previous.ChunkIndex == -1 ? previous.Revision : null;
        int? chunkIndex = previous.ChunkIndex == -1 ? -1 : null;
        return filter.Eq(d => d.Id, previous.Id)
            & filter.Eq(d => d.UpdatedAt, previous.UpdatedAt)
            & filter.Eq(d => d.ChunkIndex, chunkIndex)
            & (revision == null
                ? filter.Eq(d => d.Revision, null)
                : filter.Eq(d => d.Revision, revision));
    }

    private static SnapshotPayloadPointer? DisplacedPointer(DomainSnapshotDocument? previous)
        => previous == null ? null : TryReadSnapshotPayloadPointer(previous.Data);

    private Task ReclaimDisplacedBlobAsync(Guid runId, string domain, DomainSnapshotDocument? previous)
    {
        var displaced = DisplacedPointer(previous);
        return displaced == null
            ? Task.CompletedTask
            : DeleteDisplacedBlobIfUnreferencedAsync(runId, domain, displaced);
    }

    private void LogDroppedNewerHeader(Guid runId, string domain)
    {
        _logger.LogWarning(
            "Snapshot domain {Domain} for run {RunId} dropped this write because the stored header is newer.",
            domain.SanitizeForLog(),
            runId.ToString().SanitizeForLog());
    }

    private async Task DeleteDisplacedBlobIfUnreferencedAsync(Guid runId, string domain, SnapshotPayloadPointer pointer)
    {
        try
        {
            using var timeout = StartCleanupLookupTimeout();
            var filter = Builders<DomainSnapshotDocument>.Filter;
            var docs = await _snapshots.Find(filter.Eq(d => d.RunId, runId) & filter.Eq(d => d.Domain, domain))
                .ToListAsync(timeout.Token);
            var referenced = docs.Any(doc =>
            {
                var found = TryReadSnapshotPayloadPointer(doc.Data);
                return found != null && string.Equals(found.BlobName, pointer.BlobName, StringComparison.Ordinal);
            });
            if (referenced)
                return;

            await _snapshotPayloadStore.DeleteIfExistsAsync(pointer, timeout.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Snapshot blob cleanup failed for domain {Domain} run {RunId}.",
                domain.SanitizeForLog(),
                runId.ToString().SanitizeForLog());
        }
    }

    private async Task DeleteUnusedSnapshotBlobAsync(Guid runId, string domain, SnapshotPayloadPointer pointer)
    {
        try
        {
            using var timeout = StartCleanupLookupTimeout();
            await _snapshotPayloadStore.DeleteIfExistsAsync(pointer, timeout.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Deleting an unused snapshot blob for domain {Domain} run {RunId} failed.",
                domain.SanitizeForLog(),
                runId.ToString().SanitizeForLog());
        }
    }

    /// <summary>
    /// Cleanup after a failed header write keeps going when the caller cancels.
    /// The timeout is the only bound, so a hung lookup cannot sit forever.
    /// </summary>
    private static CancellationTokenSource StartCleanupLookupTimeout()
        => new(TimeSpan.FromSeconds(15));

    private static bool LosesTo(DomainSnapshotDocument header, ObjectId keepId, DateTimeOffset now)
        => header.UpdatedAt < now || (header.UpdatedAt == now && header.Id.CompareTo(keepId) < 0);

    /// <summary>
    /// Deletes slices for <paramref name="revision"/> when no header still
    /// references it. A failed lookup deletes nothing.
    /// </summary>
    private async Task ReclaimUnreferencedRevisionAsync(Guid runId, string domain, string? revision)
    {
        if (string.IsNullOrEmpty(revision))
            return;

        try
        {
            using var timeout = StartCleanupLookupTimeout();
            var headers = await FindHeadersAsync(runId, domain, timeout.Token);
            if (headers.Any(header => header.ChunkIndex == -1 && header.Revision == revision))
                return;

            await DeleteSlicesAsync(runId, domain, revision, timeout.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Snapshot revision cleanup failed for domain {Domain} run {RunId}.",
                domain.SanitizeForLog(),
                runId.ToString().SanitizeForLog());
        }
    }

    private Task DeleteSlicesAsync(Guid runId, string domain, string revision, CancellationToken ct)
    {
        var filter = Builders<DomainSnapshotDocument>.Filter;
        var slices = filter.Eq(d => d.RunId, runId)
            & filter.Eq(d => d.Domain, domain)
            & filter.Gte(d => d.ChunkIndex, 0)
            & filter.Eq(d => d.Revision, revision);
        return _snapshots.DeleteManyAsync(slices, ct);
    }

    private async Task DeleteSlicesQuietlyAsync(Guid runId, string domain, string revision)
    {
        try
        {
            using var timeout = StartCleanupLookupTimeout();
            await DeleteSlicesAsync(runId, domain, revision, timeout.Token);
        }
        catch (Exception cleanupEx)
        {
            _logger.LogWarning(
                cleanupEx,
                "Snapshot chunk cleanup failed for domain {Domain} run {RunId}.",
                domain.SanitizeForLog(),
                runId.ToString().SanitizeForLog());
        }
    }

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
                var currentChunk = await _logs.Find(CreateLogChunkFilter(runId))
                    .SortByDescending(l => l.Id)
                    .FirstOrDefaultAsync(ct);

                if (currentChunk != null && currentChunk.BsonByteCount == 0 && currentChunk.LineCount > 0)
                {
                    var estimatedChunkBsonBytes = EstimateChunkLinesBsonBytes(currentChunk.Lines);
                    var initializeBsonBytesFilter = Builders<RunLogDocument>.Filter.And(
                        Builders<RunLogDocument>.Filter.Eq(l => l.Id, currentChunk.Id),
                        Builders<RunLogDocument>.Filter.Or(
                            Builders<RunLogDocument>.Filter.Eq(l => l.BsonByteCount, 0),
                            Builders<RunLogDocument>.Filter.Exists(nameof(RunLogDocument.BsonByteCount), false)));

                    var initializeBsonBytesResult = await _logs.UpdateOneAsync(
                        initializeBsonBytesFilter,
                        Builders<RunLogDocument>.Update.Set(l => l.BsonByteCount, estimatedChunkBsonBytes),
                        cancellationToken: ct);

                    if (initializeBsonBytesResult.ModifiedCount == 1)
                    {
                        currentChunk.BsonByteCount = estimatedChunkBsonBytes;
                    }
                    else
                    {
                        continue;
                    }
                }

                var currentChunkEstimatedBsonBytes = currentChunk?.BsonByteCount ?? 0;
                if (currentChunk == null
                    || currentChunk.LineCount >= MaxLogLinesPerChunk
                    || currentChunkEstimatedBsonBytes + lineEstimatedBsonBytes > MaxLogChunkEstimatedBsonBytes
                    || (currentChunk.LineSequences?.Count ?? 0) != currentChunk.LineCount)
                {
                    var nextChunkNumber = currentChunk?.ChunkNumber + 1 ?? 0;
                    var nextChunk = new RunLogDocument
                    {
                        Id = CreateLogChunkId(runId, nextChunkNumber),
                        RunId = runId,
                        ChunkNumber = nextChunkNumber,
                        LineCount = 1,
                        BsonByteCount = lineEstimatedBsonBytes,
                        Lines = [line],
                        LineSequences = [lineSequence],
                        UpdatedAt = DateTimeOffset.UtcNow
                    };

                    try
                    {
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
                    Builders<RunLogDocument>.Filter.Lte(l => l.BsonByteCount, MaxLogChunkEstimatedBsonBytes - lineEstimatedBsonBytes));
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
        var existingLineCount = (legacyLog?.Lines.Count ?? 0) + chunks.Sum(c => c.LineCount);

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

        var suffixBytes = Encoding.UTF8.GetByteCount(OversizedLogLineSuffix);
        var maxLineContentBytes = Math.Max(0, MaxLogChunkEstimatedBsonBytes - EstimatedBsonBytesPerLineOverhead - suffixBytes);
        var truncatedLine = TruncateToUtf8ByteCount(line, maxLineContentBytes);

        _logger.LogWarning(
            "[Store] AppendLogs: truncated oversized log line for run={RunId} from {OriginalBytes} to {PersistedBytes} bytes",
            runId,
            Encoding.UTF8.GetByteCount(line),
            Encoding.UTF8.GetByteCount(truncatedLine));

        return truncatedLine + OversizedLogLineSuffix;
    }

    private static int EstimateLogLineBsonBytes(string line)
        => Encoding.UTF8.GetByteCount(line) + EstimatedBsonBytesPerLineOverhead;

    private static int EstimateChunkLinesBsonBytes(IReadOnlyList<string> lines)
    {
        var total = 0;
        foreach (var chunkLine in lines)
            total += EstimateLogLineBsonBytes(chunkLine);

        return total;
    }

    private static string TruncateToUtf8ByteCount(string value, int maxBytes)
    {
        if (string.IsNullOrEmpty(value) || maxBytes <= 0)
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        var usedBytes = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            var runeBytes = rune.Utf8SequenceLength;
            if (usedBytes + runeBytes > maxBytes)
                break;

            builder.Append(rune.ToString());
            usedBytes += runeBytes;
        }

        return builder.ToString();
    }

    public async Task<List<string>> GetLogsAsync(Guid runId, CancellationToken ct = default)
    {
        var legacyLog = await _logs.Find(l => l.Id == runId.ToString()).FirstOrDefaultAsync(ct);
        var chunks = await _logs.Find(CreateLogChunkFilter(runId))
            .SortBy(l => l.Id)
            .ToListAsync(ct);

        var orderedLines = new List<(long Sequence, int Ordinal, string Line)>();
        var fallbackSequence = 0L;
        var ordinal = 0;

        if (legacyLog != null)
        {
            foreach (var line in legacyLog.Lines)
                orderedLines.Add((fallbackSequence++, ordinal++, line));
        }

        foreach (var chunk in chunks)
        {
            var chunkSequences = chunk.LineSequences ?? [];
            for (var i = 0; i < chunk.Lines.Count; i++)
            {
                if (i < chunkSequences.Count)
                {
                    var sequence = chunkSequences[i];
                    orderedLines.Add((sequence, ordinal++, chunk.Lines[i]));
                    if (fallbackSequence <= sequence)
                        fallbackSequence = sequence + 1;
                }
                else
                {
                    orderedLines.Add((fallbackSequence++, ordinal++, chunk.Lines[i]));
                }
            }
        }

        return orderedLines
            .OrderBy(l => l.Sequence)
            .ThenBy(l => l.Ordinal)
            .Select(l => l.Line)
            .ToList();
    }

    private static FilterDefinition<RunLogDocument> CreateLogChunkFilter(Guid runId)
    {
        var prefix = CreateLogChunkPrefix(runId);
        return Builders<RunLogDocument>.Filter.And(
            Builders<RunLogDocument>.Filter.Gte(l => l.Id, prefix),
            Builders<RunLogDocument>.Filter.Lt(l => l.Id, prefix + '\uffff'));
    }

    private static string CreateLogChunkId(Guid runId, int chunkNumber) => $"{CreateLogChunkPrefix(runId)}{chunkNumber:D8}";

    private static string CreateLogChunkPrefix(Guid runId) => $"{runId:N}:";
}
