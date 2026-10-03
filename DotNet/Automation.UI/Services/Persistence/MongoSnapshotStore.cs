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
///   automation_runs       — lightweight run metadata
///   automation_snapshots  — per-run, per-domain polling data (upsert on RunId+Domain)
///   automation_logs       — full log output per run
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
    private const int EstimatedBsonBytesPerLineOverhead = 64;
    private const string OversizedLogLineSuffix = " [truncated: exceeded log chunk byte budget]";
    private const string SnapshotPayloadPointerEnvelopeProperty = "__externalSnapshotPayloadPointer";

    private readonly IMongoCollection<AutomationRunDocument> _runs;
    private readonly IMongoCollection<AutomationRunInputDocument> _runInputs;
    private readonly IMongoCollection<DomainSnapshotDocument> _snapshots;
    private readonly IMongoCollection<SnapshotPartDocument> _parts;
    private readonly IMongoCollection<RunLogDocument> _logs;
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
        await DeletePartsAsync(runId, ct);
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
        var plan = SnapshotPartitioner.Plan(json);

        var filter = Builders<DomainSnapshotDocument>.Filter.Eq(d => d.RunId, runId)
            & Builders<DomainSnapshotDocument>.Filter.Eq(d => d.Domain, domain);
        var existing = await _snapshots.Find(filter).FirstOrDefaultAsync(ct);
        var existingPointer = TryReadSnapshotPayloadPointer(existing?.Data);

        if (plan is SnapshotPlan.Inline inline)
        {
            SnapshotPartitioner.EnsureWithinHardCap(inline.Json);
            var before = await ReplaceHeaderAsync(filter, runId, domain, inline.Json, upsert: true, ct);
            await DeleteReplacedGenerationAsync(runId, domain, inline.Json, before, ct);
        }
        else if (plan is SnapshotPlan.Partitioned partitioned)
        {
            await CommitPartitionAsync(filter, runId, domain, partitioned, ct);
        }

        if (existingPointer != null)
            await _snapshotPayloadStore.DeleteIfExistsAsync(existingPointer, ct);
    }

    public async Task<DomainSnapshot<T>?> GetDomainAsync<T>(Guid runId, string domain, CancellationToken ct = default)
    {
        var filter = Builders<DomainSnapshotDocument>.Filter.Eq(d => d.RunId, runId)
            & Builders<DomainSnapshotDocument>.Filter.Eq(d => d.Domain, domain);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var doc = await _snapshots.Find(filter).FirstOrDefaultAsync(ct);
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

        var pointer = TryReadSnapshotPayloadPointer(legacy.Data);
        var payloadJson = legacy.Data;
        if (pointer != null)
        {
            payloadJson = await _snapshotPayloadStore.ReadAsync(pointer, ct);
            if (string.IsNullOrWhiteSpace(payloadJson))
                return false;
        }

        var plan = SnapshotPartitioner.Plan(payloadJson);
        // Match id and timestamp. The legacy payload can already be near 2 MB,
        // and putting it in the update filter makes the command exceed the cap.
        var filter = Builders<DomainSnapshotDocument>.Filter.Eq(d => d.Id, legacy.Id)
            & Builders<DomainSnapshotDocument>.Filter.Eq(d => d.UpdatedAt, legacy.UpdatedAt);
        var domainFilter = Builders<DomainSnapshotDocument>.Filter.Eq(d => d.RunId, legacy.RunId)
            & Builders<DomainSnapshotDocument>.Filter.Eq(d => d.Domain, legacy.Domain);

        if (plan is SnapshotPlan.Inline inline)
        {
            SnapshotPartitioner.EnsureWithinHardCap(inline.Json);
            var before = await ReplaceHeaderAsync(filter, legacy.RunId, legacy.Domain, inline.Json, upsert: false, ct);
            if (before == null)
                return false;

            await DeleteReplacedGenerationAsync(legacy.RunId, legacy.Domain, inline.Json, before, ct);
            if (pointer != null)
                await _snapshotPayloadStore.DeleteIfExistsAsync(pointer, ct);
            return true;
        }

        if (plan is not SnapshotPlan.Partitioned partitioned)
            return false;

        var committed = await CommitPartitionAsync(domainFilter, legacy.RunId, legacy.Domain, partitioned, ct, filter);
        if (!committed)
            return false;

        if (pointer != null)
            await _snapshotPayloadStore.DeleteIfExistsAsync(pointer, ct);
        return true;
    }

    public async Task<int> SplitOversizedLogChunksAsync(CancellationToken ct = default)
    {
        await RewriteLegacyLogByteCountsAsync(ct);
        var split = 0;
        var oversized = await _logs.Find(l => l.BsonByteCount > MaxLogChunkEstimatedBsonBytes
                || l.LineCount > MaxLogLinesPerChunk)
            .Limit(25)
            .ToListAsync(ct);

        foreach (var chunk in oversized)
        {
            ct.ThrowIfCancellationRequested();
            if (chunk.Lines.Count == 0)
                continue;

            var planned = PlanLogSplit(chunk, await FallbackSequenceBeforeChunkAsync(chunk, ct));
            if (planned.Count == 0)
                continue;

            var start = await ClaimLogSplitStartAsync(chunk, planned.Count, ct);
            var wrote = false;
            for (var attempt = 0; attempt < 5 && !wrote; attempt++)
            {
                var collided = false;
                for (var n = 0; n < planned.Count; n++)
                {
                    var document = planned[n];
                    document.Id = CreateLogChunkId(chunk.RunId, start + n);
                    document.ChunkNumber = start + n;
                    document.SplitFromId = chunk.Id;
                    document.UpdatedAt = DateTimeOffset.UtcNow;
                    EnsureLogChunkWithinCap(document);

                    var existing = await _logs.Find(l => l.Id == document.Id).FirstOrDefaultAsync(ct);
                    if (existing != null && existing.SplitFromId != chunk.Id)
                    {
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
                    else
                    {
                        await CosmosThrottle.ExecuteAsync(
                            token => _logs.ReplaceOneAsync(l => l.Id == document.Id, document, cancellationToken: token),
                            ct,
                            _logger);
                    }
                }

                if (!collided)
                {
                    wrote = true;
                    break;
                }

                await CosmosThrottle.ExecuteAsync(
                    token => _logs.DeleteManyAsync(l => l.SplitFromId == chunk.Id, token),
                    ct,
                    _logger);
                chunk.SplitStart = null;
                start = await ClaimLogSplitStartAsync(chunk, planned.Count, ct);
            }

            if (!wrote)
            {
                throw new InvalidOperationException(
                    $"Could not split log chunk '{chunk.Id.SanitizeForLog()}' without overwriting another chunk.");
            }

            await CosmosThrottle.ExecuteAsync(
                token => _logs.DeleteOneAsync(l => l.Id == chunk.Id, token),
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

    private async Task<int> ClaimLogSplitStartAsync(RunLogDocument chunk, int replacementCount, CancellationToken ct)
    {
        if (chunk.SplitStart is int existing
            && !await LogSplitRangeCollidesAsync(chunk, existing, replacementCount, ct))
        {
            return existing;
        }

        var maxChunkNumber = await _logs.Find(l => l.RunId == chunk.RunId)
            .SortByDescending(l => l.ChunkNumber)
            .Project(l => l.ChunkNumber)
            .FirstOrDefaultAsync(ct);
        var candidate = maxChunkNumber + 1;
        if (chunk.SplitStart is int prior)
            candidate = Math.Max(candidate, prior + Math.Max(replacementCount, 1));

        while (await LogSplitRangeCollidesAsync(chunk, candidate, replacementCount, ct))
            candidate += Math.Max(replacementCount, 1);

        var start = candidate;
        await CosmosThrottle.ExecuteAsync(
            token => _logs.UpdateOneAsync(
                l => l.Id == chunk.Id,
                Builders<RunLogDocument>.Update.Set(l => l.SplitStart, start),
                cancellationToken: token),
            ct,
            _logger);
        chunk.SplitStart = start;
        return start;
    }

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

    private Task<DomainSnapshotDocument?> ReplaceHeaderAsync(
        FilterDefinition<DomainSnapshotDocument> filter,
        Guid runId,
        string domain,
        string data,
        bool upsert,
        CancellationToken ct)
    {
        var update = Builders<DomainSnapshotDocument>.Update
            .Set(d => d.Data, data)
            .Set(d => d.UpdatedAt, DateTimeOffset.UtcNow)
            .SetOnInsert(d => d.RunId, runId)
            .SetOnInsert(d => d.Domain, domain);
        var options = new FindOneAndUpdateOptions<DomainSnapshotDocument>
        {
            IsUpsert = upsert,
            ReturnDocument = ReturnDocument.Before
        };
        return CosmosThrottle.ExecuteAsync(
            async token => (DomainSnapshotDocument?)await _snapshots.FindOneAndUpdateAsync(filter, update, options, token),
            ct,
            _logger);
    }

    private async Task<bool> CommitPartitionAsync(
        FilterDefinition<DomainSnapshotDocument> upsertFilter,
        Guid runId,
        string domain,
        SnapshotPlan.Partitioned partitioned,
        CancellationToken ct,
        FilterDefinition<DomainSnapshotDocument>? compareAndSwap = null)
    {
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

            var update = Builders<DomainSnapshotDocument>.Update
                .Set(d => d.Data, headerJson)
                .Set(d => d.UpdatedAt, DateTimeOffset.UtcNow)
                .SetOnInsert(d => d.RunId, runId)
                .SetOnInsert(d => d.Domain, domain);
            var options = new FindOneAndUpdateOptions<DomainSnapshotDocument>
            {
                IsUpsert = compareAndSwap == null,
                ReturnDocument = ReturnDocument.Before
            };
            var before = await CosmosThrottle.ExecuteAsync(
                token => _snapshots.FindOneAndUpdateAsync(compareAndSwap ?? upsertFilter, update, options, token),
                ct,
                _logger);
            if (compareAndSwap != null && before == null)
            {
                await DeleteGenerationAsync(runId, domain, generationId, ct);
                return false;
            }

            var previousGeneration = GenerationIdOf(before?.Data);
            if (await GenerationIsCommittedAsync(upsertFilter, runId, domain, generationId, ct))
            {
                // Delete only the generation this commit replaced. A writer that
                // commits after us has a different generation id, and deleting
                // every other generation would remove that writer's parts.
                if (previousGeneration != null)
                    await DeleteGenerationAsync(runId, domain, previousGeneration, ct);

                if (await GenerationIsCommittedAsync(upsertFilter, runId, domain, generationId, ct))
                    return true;
            }

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
            SnapshotPartitioner.EnsureWithinHardCap(piece.Data);
            var estimate = SnapshotPartitioner.EstimateStoredDocumentBytes(piece.Data);
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
                ItemKey = piece.ItemKey,
                Data = piece.Data,
                UpdatedAt = batchStamp
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
    /// Deletes the generation the header write replaced, and only while this
    /// inline payload is still the header. A partitioned commit that landed
    /// after the inline write keeps its own generation.
    /// </summary>
    private async Task DeleteReplacedGenerationAsync(
        Guid runId,
        string domain,
        string inlineJson,
        DomainSnapshotDocument? before,
        CancellationToken ct)
    {
        var previousGeneration = GenerationIdOf(before?.Data);
        if (previousGeneration == null)
            return;

        var current = await _snapshots.Find(d => d.RunId == runId && d.Domain == domain).FirstOrDefaultAsync(ct);
        if (current == null
            || SnapshotPartitionHeader.TryRead(current.Data, out _)
            || !string.Equals(current.Data, inlineJson, StringComparison.Ordinal))
        {
            return;
        }

        await DeleteGenerationAsync(runId, domain, previousGeneration, ct);
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

    private async Task<bool> GenerationIsCommittedAsync(
        FilterDefinition<DomainSnapshotDocument> filter,
        Guid runId,
        string domain,
        string generationId,
        CancellationToken ct)
    {
        var committed = await _snapshots.Find(filter).FirstOrDefaultAsync(ct);
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
                    var nextChunkNumber = currentChunk?.ChunkNumber + 1 ?? 0;
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

        var kept = line;
        while (kept.Length > 0
               && EstimateLogLineBsonBytes(kept + OversizedLogLineSuffix) > MaxLogChunkEstimatedBsonBytes)
        {
            var runeCount = 0;
            foreach (var _ in kept.EnumerateRunes())
                runeCount++;

            kept = TakeRunes(kept, Math.Max(0, runeCount - Math.Max(1, runeCount / 8)));
        }

        _logger.LogWarning(
            "[Store] AppendLogs: truncated oversized log line for run={RunId} from {OriginalBytes} to {PersistedBytes} bytes",
            runId.ToString().SanitizeForLog(),
            Encoding.UTF8.GetByteCount(line),
            Encoding.UTF8.GetByteCount(kept));

        return kept + OversizedLogLineSuffix;
    }

    private static string TakeRunes(string value, int runeCount)
    {
        var builder = new StringBuilder(value.Length);
        var taken = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            if (taken >= runeCount)
                break;

            builder.Append(rune.ToString());
            taken++;
        }

        return builder.ToString();
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
        var legacyLog = await _logs.Find(l => l.Id == runId.ToString()).FirstOrDefaultAsync(ct);
        var chunks = await _logs.Find(CreateLogChunkFilter(runId))
            .SortBy(l => l.Id)
            .ToListAsync(ct);

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

        var existingIds = new HashSet<string>(StringComparer.Ordinal) { chunk.Id };
        if (legacy != null)
            existingIds.Add(legacy.Id);
        foreach (var item in earlier)
            existingIds.Add(item.Id);

        var fallback = 0L;
        if (legacy != null)
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

    private async Task RewriteLegacyLogByteCountsAsync(CancellationToken ct)
    {
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
                return;

            afterId = chunk.Id;
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
