using MongoDB.Driver;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Automation.UI.Services.Persistence;

public sealed class PatientBundleExternalizationMigrationService : IHostedService
{
    private readonly IMongoCollection<ImportedBundleDocument> _bundles;
    private readonly IMongoCollection<TestScenarioDocument> _scenarios;
    private readonly IMongoCollection<AutomationRunInputDocument> _runInputs;
    private readonly IImportedBundleContentStore _contentStore;
    private readonly ILogger<PatientBundleExternalizationMigrationService> _logger;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private const int MigrationBatchSize = 25;
    private static readonly TimeSpan InterBatchPause = TimeSpan.FromMilliseconds(250);
    private const long MaxDocsForDeepDedupPass = 5000;

    public PatientBundleExternalizationMigrationService(
        IMongoDatabase database,
        IImportedBundleContentStore contentStore,
        ILogger<PatientBundleExternalizationMigrationService> logger,
        IHostApplicationLifetime applicationLifetime)
    {
        _bundles = database.GetCollection<ImportedBundleDocument>("automation_imported_bundles");
        _scenarios = database.GetCollection<TestScenarioDocument>("automation_scenarios");
        _runInputs = database.GetCollection<AutomationRunInputDocument>("automation_run_inputs");
        _contentStore = contentStore;
        _logger = logger;
        _applicationLifetime = applicationLifetime;
    }

    public Task StartAsync(CancellationToken cancellationToken) => RunMigrationAsync(cancellationToken);

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }

    private async Task RunMigrationAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting required migration of imported bundles to Azure Blob Storage.");

            var migratedEmbeddedPayloads = await MigrateEmbeddedPayloadsAsync(stoppingToken);
            var migratedPayloadDocs = await MigrateInlinePayloadsToAbsAsync(stoppingToken);
            var deduplicatedDocs = await DeduplicateBundleDocumentsAsync(stoppingToken);

            _logger.LogInformation(
                "Imported bundle ABS migration completed. Embedded payloads migrated: {MigratedEmbeddedPayloads}. Payload docs migrated: {MigratedPayloadDocs}. Duplicate bundle docs removed: {DeduplicatedDocs}.",
                migratedEmbeddedPayloads,
                migratedPayloadDocs,
                deduplicatedDocs);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Imported bundle ABS migration cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Imported bundle ABS migration failed. Application startup is blocked to prevent execution against inline bundle payloads.");
            throw;
        }
    }

    private async Task<int> MigrateEmbeddedPayloadsAsync(CancellationToken ct)
    {
        var migrated = 0;
        var scenarios = await _scenarios.Find(FilterDefinition<TestScenarioDocument>.Empty).ToListAsync(ct);
        foreach (var scenario in scenarios)
        {
            var result = await ExternalizeBundleJsonAsync(scenario.ImportedPatientBundlesJson, scenario.Id, ct);
            if (!result.Changed)
                continue;

            await _scenarios.UpdateOneAsync(
                s => s.Id == scenario.Id,
                Builders<TestScenarioDocument>.Update
                    .Set(s => s.ImportedPatientBundlesJson, result.Json)
                    .Set(s => s.ImportedBundleRefs, result.References)
                    .Set(s => s.UpdatedAt, DateTimeOffset.UtcNow),
                cancellationToken: ct);
            migrated += result.MigratedCount;
        }

        var runInputs = await _runInputs.Find(FilterDefinition<AutomationRunInputDocument>.Empty).ToListAsync(ct);
        foreach (var runInput in runInputs)
        {
            var result = await ExternalizeBundleJsonAsync(runInput.RunConfigurationJson, scenarioId: null, ct);
            if (!result.Changed)
                continue;

            await _runInputs.UpdateOneAsync(
                r => r.RunId == runInput.RunId,
                Builders<AutomationRunInputDocument>.Update
                    .Set(r => r.RunConfigurationJson, result.Json)
                    .Set(r => r.ImportedBundleIds, runInput.ImportedBundleIds.Concat(result.References.Select(reference => reference.BundleId)).Distinct().ToList())
                    .Set(r => r.UpdatedAt, DateTimeOffset.UtcNow),
                cancellationToken: ct);
            migrated += result.MigratedCount;
        }

        return migrated;
    }

    private async Task<(bool Changed, string? Json, List<ImportedBundleReference> References, int MigratedCount)> ExternalizeBundleJsonAsync(
        string? json,
        Guid? scenarioId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(json))
            return (false, json, [], 0);

        var root = JsonNode.Parse(json);
        var bundles = root switch
        {
            JsonArray array => array,
            JsonObject obj when obj["importedPatientBundles"] is JsonArray array => array,
            _ => null
        };
        if (bundles == null)
            return (false, json, [], 0);

        var references = new List<ImportedBundleReference>();
        var migrated = 0;
        foreach (var bundle in bundles.OfType<JsonObject>())
        {
            var rawJson = bundle["bundleJson"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(rawJson))
                continue;

            var contentHash = ComputeContentHash(rawJson);
            var now = DateTimeOffset.UtcNow;
            var document = await _bundles.FindOneAndUpdateAsync(
                b => b.ContentHash == contentHash,
                Builders<ImportedBundleDocument>.Update
                    .SetOnInsert(b => b.Id, Guid.NewGuid())
                    .SetOnInsert(b => b.ContentHash, contentHash)
                    .SetOnInsert(b => b.CreatedAt, now)
                    .SetOnInsert(b => b.IsLibraryEntry, false)
                    .Set(b => b.PatientId, bundle["patientId"]?.GetValue<string>() ?? string.Empty)
                    .Set(b => b.FileName, bundle["fileName"]?.GetValue<string>())
                    .Set(b => b.UpdatedAt, now)
                    .AddToSetEach(b => b.ScenarioIds, scenarioId.HasValue ? [scenarioId.Value] : []),
                new FindOneAndUpdateOptions<ImportedBundleDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After },
                ct);

            var stored = await _contentStore.StoreAsync(document.Id, document.ContentHash, rawJson, ct);
            await _bundles.UpdateOneAsync(
                b => b.Id == document.Id,
                Builders<ImportedBundleDocument>.Update
                    .Set(b => b.BundleBlobName, stored.BlobName)
                    .Set(b => b.ByteCount, stored.ByteCount)
                    .Set(b => b.BundleJson, null)
                    .Set(b => b.UpdatedAt, now),
                cancellationToken: ct);

            bundle["uploadedBundleId"] = document.Id.ToString();
            bundle.Remove("bundleJson");
            references.Add(new ImportedBundleReference { BundleId = document.Id, PatientId = bundle["patientId"]?.GetValue<string>() ?? string.Empty });
            migrated++;
        }

        return migrated == 0 ? (false, json, references, 0) : (true, root!.ToJsonString(), references, migrated);
    }

    private async Task<int> MigrateInlinePayloadsToAbsAsync(CancellationToken ct)
    {
        var migrated = 0;
        var failedDocIds = new HashSet<Guid>();

        var filter = Builders<ImportedBundleDocument>.Filter.And(
            Builders<ImportedBundleDocument>.Filter.Ne(b => b.BundleJson, null),
            Builders<ImportedBundleDocument>.Filter.Ne(b => b.BundleJson, string.Empty));

        while (!ct.IsCancellationRequested)
        {
            var batchFilter = failedDocIds.Count == 0
                ? filter
                : Builders<ImportedBundleDocument>.Filter.And(
                    filter,
                    Builders<ImportedBundleDocument>.Filter.Nin(b => b.Id, failedDocIds));

            var docs = await _bundles.Find(batchFilter)
                .Limit(MigrationBatchSize)
                .ToListAsync(ct);

            if (docs.Count == 0)
                break;

            var batchResult = await MigrateInlinePayloadBatchAsync(docs, failedDocIds, ct);
            migrated += batchResult.Migrated;

            if (batchResult.Migrated == 0 && batchResult.Failed > 0)
            {
                _logger.LogWarning(
                    "Imported bundle ABS migration batch made no progress ({FailedCount} failed). " +
                    "Stopping further attempts this startup; failed docs will retry on next startup.",
                    batchResult.Failed);
                break;
            }

            _logger.LogInformation("Imported bundle ABS migration progress: migrated {Migrated} inline payload document(s).", migrated);

            // Avoid sustained pressure on Mongo/ABS so foreground UI requests stay responsive.
            await Task.Delay(InterBatchPause, ct);
        }

        return migrated;
    }

    internal async Task<(int Migrated, int Failed)> MigrateInlinePayloadBatchAsync(
        IReadOnlyCollection<ImportedBundleDocument> docs,
        ISet<Guid> failedDocIds,
        CancellationToken ct)
    {
        var migrated = 0;
        var failed = 0;

        foreach (var doc in docs)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var json = doc.BundleJson;
                if (string.IsNullOrWhiteSpace(json))
                    continue;

                var contentHash = string.IsNullOrWhiteSpace(doc.ContentHash)
                    ? ComputeContentHash(json)
                    : doc.ContentHash;

                var stored = await _contentStore.StoreAsync(doc.Id, contentHash, json, ct);

                var update = Builders<ImportedBundleDocument>.Update
                    .Set(b => b.ContentHash, contentHash)
                    .Set(b => b.BundleBlobName, stored.BlobName)
                    .Set(b => b.ByteCount, stored.ByteCount)
                    .Set(b => b.UpdatedAt, DateTimeOffset.UtcNow)
                    .Set(b => b.BundleJson, null);

                await _bundles.UpdateOneAsync(b => b.Id == doc.Id, update, cancellationToken: ct);
                migrated++;
            }
            catch (Exception ex)
            {
                failed++;
                failedDocIds.Add(doc.Id);
                _logger.LogWarning(ex, "Failed migrating imported bundle {BundleId} payload to ABS. Will retry next startup.", doc.Id);
            }
        }

        return (migrated, failed);
    }

    private async Task<int> DeduplicateBundleDocumentsAsync(CancellationToken ct)
    {
        var totalDocs = await _bundles.CountDocumentsAsync(FilterDefinition<ImportedBundleDocument>.Empty, cancellationToken: ct);
        if (totalDocs > MaxDocsForDeepDedupPass)
        {
            _logger.LogInformation(
                "Skipping deep imported-bundle dedup pass for now: {DocCount} documents exceeds threshold {Threshold}. " +
                "Payload migration still completed; dedup can run during lower-load maintenance.",
                totalDocs,
                MaxDocsForDeepDedupPass);
            return 0;
        }

        await EnsureMissingContentHashesAsync(ct);

        var all = await _bundles.Find(FilterDefinition<ImportedBundleDocument>.Empty).ToListAsync(ct);
        var duplicateGroups = all
            .Where(b => !string.IsNullOrWhiteSpace(b.ContentHash))
            .GroupBy(b => b.ContentHash, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .ToList();

        if (duplicateGroups.Count == 0)
            return 0;

        var remap = new Dictionary<Guid, Guid>();
        var duplicatesToDelete = new List<ImportedBundleDocument>();
        var dedupClaimToken = Guid.NewGuid().ToString("N");
        var retainedCanonicalBlobNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var group in duplicateGroups)
        {
            var ordered = group
                .OrderBy(b => b.CreatedAt)
                .ThenBy(b => b.Id)
                .ToList();

            var canonical = ordered[0];
            var duplicates = ordered.Skip(1).ToList();
            if (duplicates.Count == 0)
                continue;

            var canonicalScenarioIds = new HashSet<Guid>(canonical.ScenarioIds);
            foreach (var dup in duplicates)
            {
                foreach (var sid in dup.ScenarioIds)
                    canonicalScenarioIds.Add(sid);
                remap[dup.Id] = canonical.Id;
                duplicatesToDelete.Add(dup);

                await _bundles.UpdateOneAsync(
                    b => b.Id == dup.Id,
                    Builders<ImportedBundleDocument>.Update
                        .Set(b => b.CanonicalBundleId, canonical.Id)
                        .Set(b => b.DeletionClaimToken, dedupClaimToken)
                        .Set(b => b.DeletionClaimedAt, DateTimeOffset.UtcNow)
                        .Set(b => b.UpdatedAt, DateTimeOffset.UtcNow),
                    cancellationToken: ct);
            }

            if (string.IsNullOrWhiteSpace(canonical.BundleBlobName))
            {
                var donor = duplicates.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d.BundleBlobName));
                if (donor != null)
                    canonical.BundleBlobName = donor.BundleBlobName;
            }

            if (canonical.ByteCount <= 0)
            {
                var donor = duplicates.FirstOrDefault(d => d.ByteCount > 0);
                if (donor != null)
                    canonical.ByteCount = donor.ByteCount;
            }

            if (!string.IsNullOrWhiteSpace(canonical.BundleBlobName))
                retainedCanonicalBlobNames.Add(canonical.BundleBlobName);

            var canonicalUpdate = Builders<ImportedBundleDocument>.Update
                .Set(b => b.ScenarioIds, canonicalScenarioIds.ToList())
                .Set(b => b.BundleBlobName, canonical.BundleBlobName)
                .Set(b => b.ByteCount, canonical.ByteCount)
                .Set(b => b.UpdatedAt, DateTimeOffset.UtcNow);

            await _bundles.UpdateOneAsync(b => b.Id == canonical.Id, canonicalUpdate, cancellationToken: ct);
        }

        if (remap.Count == 0)
            return 0;

        await RemapScenarioBundleReferencesAsync(remap, ct);
        await RemapRunInputBundleReferencesAsync(remap, ct);

        foreach (var dup in duplicatesToDelete)
        {
            if (!string.IsNullOrWhiteSpace(dup.BundleBlobName)
                && retainedCanonicalBlobNames.Contains(dup.BundleBlobName))
            {
                continue;
            }

            try
            {
                await _contentStore.DeleteAsync(dup, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed deleting duplicate imported bundle blob for document {BundleId} ({BlobName}).", dup.Id, dup.BundleBlobName);
            }
        }

        var deleteFilter = Builders<ImportedBundleDocument>.Filter.And(
            Builders<ImportedBundleDocument>.Filter.In(b => b.Id, duplicatesToDelete.Select(d => d.Id)),
            Builders<ImportedBundleDocument>.Filter.Eq(b => b.DeletionClaimToken, dedupClaimToken));
        await _bundles.DeleteManyAsync(deleteFilter, ct);

        _logger.LogInformation("Imported bundle deduplication remapped {RemapCount} reference(s) and removed {DuplicateCount} duplicate bundle document(s).", remap.Count, duplicatesToDelete.Count);
        return duplicatesToDelete.Count;
    }

    private async Task EnsureMissingContentHashesAsync(CancellationToken ct)
    {
        var filter = Builders<ImportedBundleDocument>.Filter.Or(
            Builders<ImportedBundleDocument>.Filter.Eq(b => b.ContentHash, null),
            Builders<ImportedBundleDocument>.Filter.Eq(b => b.ContentHash, string.Empty));

        var docs = await _bundles.Find(filter).ToListAsync(ct);
        if (docs.Count == 0)
            return;

        var fixedCount = 0;
        foreach (var doc in docs)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var json = await _contentStore.ReadAsync(doc, ct);
                if (string.IsNullOrWhiteSpace(json))
                    continue;

                var hash = ComputeContentHash(json);
                await _bundles.UpdateOneAsync(
                    b => b.Id == doc.Id,
                    Builders<ImportedBundleDocument>.Update
                        .Set(b => b.ContentHash, hash)
                        .Set(b => b.UpdatedAt, DateTimeOffset.UtcNow),
                    cancellationToken: ct);
                fixedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed computing content hash for imported bundle document {BundleId}; deduplication may skip it.", doc.Id);
            }
        }

        if (fixedCount > 0)
            _logger.LogInformation("Computed missing content hashes for {Count} imported bundle document(s).", fixedCount);
    }

    private async Task RemapScenarioBundleReferencesAsync(Dictionary<Guid, Guid> remap, CancellationToken ct)
    {
        var scenarios = await _scenarios.Find(FilterDefinition<TestScenarioDocument>.Empty).ToListAsync(ct);

        foreach (var scenario in scenarios)
        {
            var refsChanged = false;
            foreach (var reference in scenario.ImportedBundleRefs)
            {
                if (!remap.TryGetValue(reference.BundleId, out var replacement))
                    continue;

                reference.BundleId = replacement;
                refsChanged = true;
            }

            var jsonChanged = false;
            if (!string.IsNullOrWhiteSpace(scenario.ImportedPatientBundlesJson))
            {
                var remappedJson = RemapUploadedBundleIdsInJson(scenario.ImportedPatientBundlesJson, remap);
                if (!string.Equals(remappedJson, scenario.ImportedPatientBundlesJson, StringComparison.Ordinal))
                {
                    scenario.ImportedPatientBundlesJson = remappedJson;
                    jsonChanged = true;
                }
            }

            if (!refsChanged && !jsonChanged)
                continue;

            scenario.ImportedBundleRefs = scenario.ImportedBundleRefs
                .GroupBy(r => new { r.BundleId, PatientId = r.PatientId ?? string.Empty })
                .Select(g => g.First())
                .ToList();
            var scenarioUpdate = Builders<TestScenarioDocument>.Update
                .Set(s => s.ImportedBundleRefs, scenario.ImportedBundleRefs)
                .Set(s => s.ImportedPatientBundlesJson, scenario.ImportedPatientBundlesJson)
                .Set(s => s.UpdatedAt, DateTimeOffset.UtcNow);

            await _scenarios.UpdateOneAsync(s => s.Id == scenario.Id, scenarioUpdate, cancellationToken: ct);
        }
    }

    private async Task RemapRunInputBundleReferencesAsync(Dictionary<Guid, Guid> remap, CancellationToken ct)
    {
        var runInputs = await _runInputs.Find(FilterDefinition<AutomationRunInputDocument>.Empty).ToListAsync(ct);

        foreach (var runInput in runInputs)
        {
            var idsChanged = false;
            var newIds = runInput.ImportedBundleIds
                .Select(id =>
                {
                    if (!remap.TryGetValue(id, out var replacement))
                        return id;

                    idsChanged = true;
                    return replacement;
                })
                .Distinct()
                .ToList();

            var jsonChanged = false;
            if (!string.IsNullOrWhiteSpace(runInput.RunConfigurationJson))
            {
                var remappedJson = RemapUploadedBundleIdsInJson(runInput.RunConfigurationJson!, remap);
                if (!string.Equals(remappedJson, runInput.RunConfigurationJson, StringComparison.Ordinal))
                {
                    runInput.RunConfigurationJson = remappedJson;
                    jsonChanged = true;
                }
            }

            if (!idsChanged && !jsonChanged)
                continue;

            var runInputUpdate = Builders<AutomationRunInputDocument>.Update
                .Set(r => r.ImportedBundleIds, newIds)
                .Set(r => r.RunConfigurationJson, runInput.RunConfigurationJson)
                .Set(r => r.UpdatedAt, DateTimeOffset.UtcNow);

            await _runInputs.UpdateOneAsync(r => r.RunId == runInput.RunId, runInputUpdate, cancellationToken: ct);
        }
    }

    internal static string RemapUploadedBundleIdsInJson(string json, IReadOnlyDictionary<Guid, Guid> remap)
    {
        try
        {
            var parsed = JsonNode.Parse(json);
            if (parsed == null)
                return json;

            if (parsed is JsonArray rootArray)
            {
                var changed = RemapUploadedBundleIdsInArray(rootArray, remap);
                return changed ? rootArray.ToJsonString() : json;
            }

            if (parsed is not JsonObject rootObject)
                return json;

            JsonArray? bundles = null;
            if (rootObject.TryGetPropertyValue("importedPatientBundles", out var camelNode)
                && camelNode is JsonArray camelArray)
            {
                bundles = camelArray;
            }
            else if (rootObject.TryGetPropertyValue("ImportedPatientBundles", out var pascalNode)
                     && pascalNode is JsonArray pascalArray)
            {
                bundles = pascalArray;
            }

            if (bundles == null)
                return json;

            var arrayChanged = RemapUploadedBundleIdsInArray(bundles, remap);
            return arrayChanged ? rootObject.ToJsonString() : json;
        }
        catch
        {
            return json;
        }
    }

    private static bool RemapUploadedBundleIdsInArray(JsonArray bundles, IReadOnlyDictionary<Guid, Guid> remap)
    {
        var changed = false;

        foreach (var node in bundles.OfType<JsonObject>())
        {
            var key = node.ContainsKey("UploadedBundleId")
                ? "UploadedBundleId"
                : node.ContainsKey("uploadedBundleId")
                    ? "uploadedBundleId"
                    : null;

            if (key == null)
                continue;

            var raw = node[key]?.GetValue<string>();
            if (!Guid.TryParse(raw, out var uploadedId))
                continue;

            if (!remap.TryGetValue(uploadedId, out var replacement))
                continue;

            node[key] = replacement.ToString();
            changed = true;
        }

        return changed;
    }

    private static string ComputeContentHash(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
