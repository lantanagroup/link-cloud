using MongoDB.Driver;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Automation.UI.Services.Persistence;

public sealed class ImportedBundleBlobMigrationService : IHostedService
{
    private readonly IMongoCollection<ImportedBundleDocument> _bundles;
    private readonly IMongoCollection<TestScenarioDocument> _scenarios;
    private readonly IMongoCollection<AutomationRunInputDocument> _runInputs;
    private readonly IImportedBundleContentStore _contentStore;
    private readonly ILogger<ImportedBundleBlobMigrationService> _logger;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private const int MigrationBatchSize = 25;
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan InterBatchPause = TimeSpan.FromMilliseconds(250);
    private const long MaxDocsForDeepDedupPass = 5000;
    private Task? _backgroundTask;
    private CancellationTokenSource? _cts;

    public ImportedBundleBlobMigrationService(
        IMongoDatabase database,
        IImportedBundleContentStore contentStore,
        ILogger<ImportedBundleBlobMigrationService> logger,
        IHostApplicationLifetime applicationLifetime)
    {
        _bundles = database.GetCollection<ImportedBundleDocument>("automation_imported_bundles");
        _scenarios = database.GetCollection<TestScenarioDocument>("automation_scenarios");
        _runInputs = database.GetCollection<AutomationRunInputDocument>("automation_run_inputs");
        _contentStore = contentStore;
        _logger = logger;
        _applicationLifetime = applicationLifetime;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _applicationLifetime.ApplicationStarted.Register(() =>
        {
            if (_cts?.IsCancellationRequested == true)
                return;

            _backgroundTask = Task.Run(() => RunMigrationAsync(_cts!.Token), CancellationToken.None);
        });

        _logger.LogInformation("Imported bundle ABS migration is scheduled to run after app startup.");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts != null)
            _cts.Cancel();

        if (_backgroundTask == null)
            return;

        await Task.WhenAny(_backgroundTask, Task.Delay(Timeout.Infinite, cancellationToken));
    }

    private async Task RunMigrationAsync(CancellationToken stoppingToken)
    {
        // Non-blocking startup: run migration in the background after app is already serving.
        await Task.Yield();

        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            _logger.LogInformation("Starting background migration of imported bundles to Azure Blob Storage.");

            var migratedPayloadDocs = await MigrateInlinePayloadsToAbsAsync(stoppingToken);
            var deduplicatedDocs = await DeduplicateBundleDocumentsAsync(stoppingToken);

            _logger.LogInformation(
                "Imported bundle ABS migration completed. Payload docs migrated: {MigratedPayloadDocs}. Duplicate bundle docs removed: {DeduplicatedDocs}.",
                migratedPayloadDocs,
                deduplicatedDocs);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Imported bundle ABS migration cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Imported bundle ABS migration encountered an error. Existing data remains usable; migration will retry on next startup.");
        }
    }

    private async Task<int> MigrateInlinePayloadsToAbsAsync(CancellationToken ct)
    {
        var migrated = 0;

        var filter = Builders<ImportedBundleDocument>.Filter.And(
            Builders<ImportedBundleDocument>.Filter.Ne(b => b.BundleJson, null),
            Builders<ImportedBundleDocument>.Filter.Ne(b => b.BundleJson, string.Empty));

        while (!ct.IsCancellationRequested)
        {
            var docs = await _bundles.Find(filter)
                .SortBy(b => b.CreatedAt)
                .Limit(MigrationBatchSize)
                .ToListAsync(ct);

            if (docs.Count == 0)
                break;

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
                    _logger.LogWarning(ex, "Failed migrating imported bundle {BundleId} payload to ABS. Will retry next startup.", doc.Id);
                }
            }

            _logger.LogInformation("Imported bundle ABS migration progress: migrated {Migrated} inline payload document(s).", migrated);

            // Avoid sustained pressure on Mongo/ABS so foreground UI requests stay responsive.
            await Task.Delay(InterBatchPause, ct);
        }

        return migrated;
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
            try
            {
                await _contentStore.DeleteAsync(dup, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed deleting duplicate imported bundle blob for document {BundleId} ({BlobName}).", dup.Id, dup.BundleBlobName);
            }
        }

        await _bundles.DeleteManyAsync(Builders<ImportedBundleDocument>.Filter.In(b => b.Id, duplicatesToDelete.Select(d => d.Id)), ct);

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

    private static string RemapUploadedBundleIdsInJson(string json, IReadOnlyDictionary<Guid, Guid> remap)
    {
        try
        {
            var root = JsonNode.Parse(json) as JsonObject;
            if (root == null)
                return json;

            if (root["importedPatientBundles"] is not JsonArray bundles)
                return json;

            var changed = false;
            foreach (var node in bundles.OfType<JsonObject>())
            {
                var raw = node["uploadedBundleId"]?.GetValue<string>();
                if (!Guid.TryParse(raw, out var uploadedId))
                    continue;

                if (!remap.TryGetValue(uploadedId, out var replacement))
                    continue;

                node["uploadedBundleId"] = replacement.ToString();
                changed = true;
            }

            return changed ? root.ToJsonString() : json;
        }
        catch
        {
            return json;
        }
    }

    private static string ComputeContentHash(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
