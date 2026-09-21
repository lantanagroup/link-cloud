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
    private const int MaxLogChunkEstimatedBsonBytes = 12 * 1024 * 1024;
    private const int EstimatedBsonBytesPerLineOverhead = 64;
    private const string OversizedLogLineSuffix = " [truncated: exceeded log chunk byte budget]";
    private const string SnapshotPayloadPointerEnvelopeProperty = "__externalSnapshotPayloadPointer";

    private readonly IMongoCollection<AutomationRunDocument> _runs;
    private readonly IMongoCollection<AutomationRunInputDocument> _runInputs;
    private readonly IMongoCollection<DomainSnapshotDocument> _snapshots;
    private readonly IMongoCollection<RunLogDocument> _logs;
    private readonly IMongoCollection<RunLogSequenceDocument> _logSequences;
    private readonly IMongoCollection<ImportedBundleDocument> _importedBundles;
    private readonly ISnapshotPayloadStore _snapshotPayloadStore;
    private readonly ILogger<MongoSnapshotStore> _logger;

    public MongoSnapshotStore(IMongoDatabase database, ILogger<MongoSnapshotStore> logger, ISnapshotPayloadStore? snapshotPayloadStore = null)
    {
        _runs = database.GetCollection<AutomationRunDocument>("automation_runs");
        _runInputs = database.GetCollection<AutomationRunInputDocument>("automation_run_inputs");
        _snapshots = database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        _logs = database.GetCollection<RunLogDocument>("automation_logs");
        _logSequences = database.GetCollection<RunLogSequenceDocument>("automation_log_sequences");
        _importedBundles = database.GetCollection<ImportedBundleDocument>("automation_imported_bundles");
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

    public async Task DeleteRunAsync(Guid runId, CancellationToken ct = default)
    {
        await _runs.DeleteOneAsync(r => r.RunId == runId, ct);
        await _runInputs.DeleteOneAsync(r => r.RunId == runId, ct);
        await _snapshots.DeleteManyAsync(s => s.RunId == runId, ct);
        await _logs.DeleteManyAsync(CreateLogChunkFilter(runId), ct);
        await _logs.DeleteOneAsync(l => l.Id == runId.ToString(), ct);

        // Only remove externalized payload blobs after Mongo cleanup succeeds so
        // a DB failure cannot orphan pointer records that still reference payload data.
        await _snapshotPayloadStore.DeleteRunPayloadsAsync(runId, ct);
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

        var filter = Builders<DomainSnapshotDocument>.Filter.Eq(d => d.RunId, runId)
            & Builders<DomainSnapshotDocument>.Filter.Eq(d => d.Domain, domain);

        var existing = await _snapshots.Find(filter).FirstOrDefaultAsync(ct);
        var existingPointer = TryReadSnapshotPayloadPointer(existing?.Data);

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

        var update = Builders<DomainSnapshotDocument>.Update
            .Set(d => d.Data, storedJson)
            .Set(d => d.UpdatedAt, DateTimeOffset.UtcNow)
            .SetOnInsert(d => d.RunId, runId)
            .SetOnInsert(d => d.Domain, domain);

        await _snapshots.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, ct);

        if (existingPointer != null && (newPointer == null || !string.Equals(existingPointer.BlobName, newPointer.BlobName, StringComparison.Ordinal)))
        {
            await _snapshotPayloadStore.DeleteIfExistsAsync(existingPointer, ct);
        }
    }

    public async Task<DomainSnapshot<T>?> GetDomainAsync<T>(Guid runId, string domain, CancellationToken ct = default)
    {
        var filter = Builders<DomainSnapshotDocument>.Filter.Eq(d => d.RunId, runId)
            & Builders<DomainSnapshotDocument>.Filter.Eq(d => d.Domain, domain);

        var doc = await _snapshots.Find(filter).FirstOrDefaultAsync(ct);
        if (doc == null)
        {
            _logger.LogDebug("[Store] GetDomain: no document for run={RunId} domain={Domain}", runId, domain);
            return null;
        }

        try
        {
            var payloadJson = doc.Data;
            var pointer = TryReadSnapshotPayloadPointer(payloadJson);
            if (pointer != null)
            {
                payloadJson = await _snapshotPayloadStore.ReadAsync(pointer, ct);
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
                _logger.LogDebug("[Store] GetDomain: deserialized to null for run={RunId} domain={Domain} (json length={Len})", runId, domain, doc.Data?.Length ?? 0);
                return null;
            }

            return new DomainSnapshot<T> { UpdatedAt = doc.UpdatedAt, Data = data };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "[Store] GetDomain: deserialization failed for run={RunId} domain={Domain} type={Type} (json length={Len})", runId, domain, typeof(T).Name, doc.Data?.Length ?? 0);
            return null;
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
