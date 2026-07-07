using System.Text.Json;
using Automation.UI.Models;
using LantanaGroup.Automation.Generation;
using MongoDB.Driver;
using System.Security.Cryptography;
using System.Text;

namespace Automation.UI.Services.Persistence;

/// <summary>
/// MongoDB-backed implementation of <see cref="IScenarioStore"/>.
///
/// <para>
/// Imported FHIR transaction bundles attached to a scenario are <b>not</b> embedded in
/// the scenario document &mdash; they are persisted to a sibling
/// <c>automation_imported_bundles</c> collection and referenced by id via
/// <see cref="TestScenarioDocument.ImportedBundleRefs"/>. This avoids breaching
/// MongoDB's 16&#8239;MB document limit when a scenario carries several large bundles
/// while keeping reads transparent to callers (the bundle JSON is hydrated back into
/// each <see cref="ImportedPatientInput.BundleJson"/> on load).
/// </para>
///
/// <para>
/// <b>Cross-scenario deduplication.</b> Bundles are content-addressed by SHA-256 of
/// their raw JSON. The same patient bundle uploaded into N scenarios is stored
/// <i>once</i>; the bundle document tracks a <see cref="ImportedBundleDocument.ScenarioIds"/>
/// set, maintained via <c>$addToSet</c> on save and <c>$pull</c> on delete /
/// bundle-removal. A bundle row is only physically deleted once every referencing
/// scenario has dropped it AND it isn't flagged as a curated library entry
/// (<see cref="ImportedBundleDocument.IsLibraryEntry"/>).
/// </para>
///
/// <para>
/// <b>Back-compat.</b> Two legacy on-disk shapes are accepted on read:
/// <list type="number">
///   <item>Pre-externalization: bundle JSON embedded inline in
///         <see cref="TestScenarioDocument.ImportedPatientBundlesJson"/> with no refs.</item>
///   <item>First externalization (single-owner): one bundle row per scenario keyed by a
///         singular <c>ScenarioId</c> field. <see cref="MongoIndexManager"/> ignores extra
///         elements via <see cref="MongoDB.Bson.Serialization.Attributes.BsonIgnoreExtraElementsAttribute"/>,
///         so the legacy field deserializes away on first read; the next
///         <see cref="UpsertAsync"/> migrates the row by populating
///         <see cref="ImportedBundleDocument.ContentHash"/> and
///         <see cref="ImportedBundleDocument.ScenarioIds"/>.</item>
/// </list>
/// </para>
/// </summary>
public sealed class MongoScenarioStore : IScenarioStore
{
    private readonly IMongoCollection<TestScenarioDocument> _scenarios;
    private readonly IMongoCollection<ImportedBundleDocument> _bundles;

    public MongoScenarioStore(IMongoDatabase database)
    {
        _scenarios = database.GetCollection<TestScenarioDocument>("automation_scenarios");
        _bundles = database.GetCollection<ImportedBundleDocument>("automation_imported_bundles");
    }

    public async Task<List<TestScenarioDefinition>> GetAllAsync(CancellationToken ct = default)
    {
        var docs = await _scenarios.Find(FilterDefinition<TestScenarioDocument>.Empty)
            .SortBy(d => d.Name)
            .ToListAsync(ct);

        // Batch-fetch all referenced bundles in a single round trip rather than
        // looking each one up per-scenario.
        var allBundleIds = docs.SelectMany(d => d.ImportedBundleRefs.Select(r => r.BundleId))
            .Distinct()
            .ToList();
        var bundlesById = await LoadBundlesByIdAsync(allBundleIds, ct);

        return docs.Select(d => ToModel(d, bundlesById)).ToList();
    }

    public async Task<TestScenarioDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var doc = await _scenarios.Find(d => d.Id == id).FirstOrDefaultAsync(ct);
        if (doc == null) return null;

        var bundleIds = doc.ImportedBundleRefs.Select(r => r.BundleId).ToList();
        var bundlesById = await LoadBundlesByIdAsync(bundleIds, ct);

        return ToModel(doc, bundlesById);
    }

    public async Task UpsertAsync(TestScenarioDefinition scenario, CancellationToken ct = default)
    {
        // Find-or-insert each bundle by content hash so identical bundles already
        // attached to other scenarios are reused rather than duplicated.
        var bundleRefs = await ResolveOrInsertBundleRefsAsync(scenario, ct);

        var sanitizedBundles = SanitizeBundlesForEmbedding(scenario.ImportedPatientBundles);

        var doc = ToDocument(scenario, sanitizedBundles, bundleRefs);
        await _scenarios.ReplaceOneAsync(
            d => d.Id == doc.Id,
            doc,
            new ReplaceOptions { IsUpsert = true },
            ct);

        // Detach this scenario from any bundle it previously referenced but doesn't anymore
        // (user removed a bundle, replaced it, or migrated a legacy inline payload). Then
        // delete bundles that no scenario references AND that aren't library-pinned.
        await DetachAndPruneOrphansAsync(scenario.Id, bundleRefs.Select(r => r.BundleId).ToHashSet(), ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        // 1) Drop the scenario document.
        await _scenarios.DeleteOneAsync(d => d.Id == id, ct);

        // 2) Detach this scenario from every bundle that referenced it; then garbage-collect
        //    bundles whose ScenarioIds set has become empty (and that aren't library entries).
        await DetachAndPruneOrphansAsync(id, keepBundleIds: new HashSet<Guid>(), ct);
    }

    /// <summary>
    /// For each in-memory bundle on the scenario, finds an existing
    /// <see cref="ImportedBundleDocument"/> with the same content hash &mdash; or upserts a
    /// new one &mdash; and ensures the scenario's id is part of the document's
    /// <see cref="ImportedBundleDocument.ScenarioIds"/> set. Returns the refs to embed
    /// on the parent <see cref="TestScenarioDocument"/>.
    /// </summary>
    private async Task<List<ImportedBundleReference>> ResolveOrInsertBundleRefsAsync(
        TestScenarioDefinition scenario,
        CancellationToken ct)
    {
        var refs = new List<ImportedBundleReference>(scenario.ImportedPatientBundles.Count);
        if (scenario.ImportedPatientBundles.Count == 0)
            return refs;

        var now = DateTimeOffset.UtcNow;

        foreach (var input in scenario.ImportedPatientBundles)
        {
            if (input.UploadedBundleId.HasValue)
            {
                var attached = await AttachExistingBundleAsync(input.UploadedBundleId.Value, input, scenario.Id, now, ct);
                if (attached == null)
                    throw new InvalidOperationException($"Failed to attach uploaded bundle '{input.UploadedBundleId.Value}' for scenario '{scenario.Id}'.");

                refs.Add(attached);
                continue;
            }

            if (string.IsNullOrWhiteSpace(input.BundleJson))
                continue; // ID-only entry mistakenly placed in the bundle list — nothing to externalize.

            var hash = ComputeContentHash(input.BundleJson!);
            var byteCount = Encoding.UTF8.GetByteCount(input.BundleJson!);

            // Single round-trip: find by hash, set immutable fields on insert, refresh
            // mutable metadata, and add this scenario id to the references set. The
            // unique index on ContentHash makes this safe under concurrent upserts.
            var update = Builders<ImportedBundleDocument>.Update
                .SetOnInsert(b => b.Id, Guid.NewGuid())
                .SetOnInsert(b => b.ContentHash, hash)
                .SetOnInsert(b => b.BundleJson, input.BundleJson!)
                .SetOnInsert(b => b.ByteCount, byteCount)
                .SetOnInsert(b => b.CreatedAt, now)
                .SetOnInsert(b => b.IsLibraryEntry, false)
                .Set(b => b.PatientId, input.PatientId ?? string.Empty)
                .Set(b => b.FileName, input.FileName)
                .Set(b => b.UpdatedAt, now)
                .AddToSet(b => b.ScenarioIds, scenario.Id);

            var options = new FindOneAndUpdateOptions<ImportedBundleDocument>
            {
                IsUpsert = true,
                ReturnDocument = ReturnDocument.After
            };

            var bundleDoc = await _bundles.FindOneAndUpdateAsync<ImportedBundleDocument>(
                b => b.ContentHash == hash, update, options, ct);

            refs.Add(new ImportedBundleReference
            {
                BundleId = bundleDoc.Id,
                PatientId = input.PatientId ?? string.Empty
            });
        }

        return refs;
    }

    private async Task<ImportedBundleReference?> AttachExistingBundleAsync(
        Guid bundleId,
        ImportedPatientInput input,
        Guid scenarioId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var update = Builders<ImportedBundleDocument>.Update
            .Set(b => b.UpdatedAt, now)
            .Set(b => b.PatientId, input.PatientId ?? string.Empty)
            .Set(b => b.FileName, input.FileName)
            .AddToSet(b => b.ScenarioIds, scenarioId);

        var bundleDoc = await _bundles.FindOneAndUpdateAsync(
            b => b.Id == bundleId,
            update,
            new FindOneAndUpdateOptions<ImportedBundleDocument> { ReturnDocument = ReturnDocument.After },
            ct);

        if (bundleDoc == null)
            return null;

        return new ImportedBundleReference
        {
            BundleId = bundleDoc.Id,
            PatientId = string.IsNullOrWhiteSpace(input.PatientId) ? bundleDoc.PatientId : input.PatientId
        };
    }

    /// <summary>
    /// Pulls <paramref name="scenarioId"/> out of every bundle's
    /// <see cref="ImportedBundleDocument.ScenarioIds"/> set <i>except</i> bundles in
    /// <paramref name="keepBundleIds"/> (the scenario's current refs); then deletes any
    /// bundle whose ScenarioIds set is now empty and which is not flagged as a library
    /// entry. This handles three cases uniformly:
    /// scenario delete (keepBundleIds empty), scenario edit that drops a bundle, and
    /// migration of legacy single-owner rows.
    /// </summary>
    private async Task DetachAndPruneOrphansAsync(
        Guid scenarioId,
        HashSet<Guid> keepBundleIds,
        CancellationToken ct)
    {
        var detachFilter = Builders<ImportedBundleDocument>.Filter.And(
            Builders<ImportedBundleDocument>.Filter.AnyEq(b => b.ScenarioIds, scenarioId),
            Builders<ImportedBundleDocument>.Filter.Nin(b => b.Id, keepBundleIds));

        await _bundles.UpdateManyAsync(
            detachFilter,
            Builders<ImportedBundleDocument>.Update.Pull(b => b.ScenarioIds, scenarioId),
            cancellationToken: ct);

        // Delete unreferenced, non-library bundles. Treats both an empty list and a missing
        // ScenarioIds field as orphan so legacy single-owner rows (without ScenarioIds) can
        // be cleaned up too.
        var orphanFilter = Builders<ImportedBundleDocument>.Filter.And(
            Builders<ImportedBundleDocument>.Filter.Or(
                Builders<ImportedBundleDocument>.Filter.Size(b => b.ScenarioIds, 0),
                Builders<ImportedBundleDocument>.Filter.Exists(b => b.ScenarioIds, false)),
            Builders<ImportedBundleDocument>.Filter.Eq(b => b.IsLibraryEntry, false));

        await _bundles.DeleteManyAsync(orphanFilter, ct);
    }

    /// <summary>
    /// Returns a copy of <paramref name="bundles"/> with each entry's
    /// <see cref="ImportedPatientInput.BundleJson"/> cleared, so the embedded list inside
    /// the scenario document carries no payload &mdash; the JSON lives in the bundles
    /// collection and is hydrated back on read.
    /// </summary>
    private static List<ImportedPatientInput> SanitizeBundlesForEmbedding(
        IReadOnlyList<ImportedPatientInput> bundles)
    {
        var sanitized = new List<ImportedPatientInput>(bundles.Count);
        foreach (var input in bundles)
        {
            if (string.IsNullOrWhiteSpace(input.BundleJson))
            {
                sanitized.Add(input);
                continue;
            }

            sanitized.Add(new ImportedPatientInput
            {
                Source = input.Source,
                PatientId = input.PatientId,
                FileName = input.FileName,
                UploadedBundleId = input.UploadedBundleId,
                BundleJson = null,
                AutoDetect = input.AutoDetect,
                MeasureEligibilities = input.MeasureEligibilities,
                DetectedClinicalScenarioId = input.DetectedClinicalScenarioId
            });
        }
        return sanitized;
    }

    private static string ComputeContentHash(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private async Task<Dictionary<Guid, string>> LoadBundlesByIdAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
            return new Dictionary<Guid, string>();

        var docs = await _bundles.Find(Builders<ImportedBundleDocument>.Filter.In(b => b.Id, ids))
            .ToListAsync(ct);

        return docs.ToDictionary(b => b.Id, b => b.BundleJson);
    }

    private static TestScenarioDocument ToDocument(
        TestScenarioDefinition model,
        List<ImportedPatientInput> sanitizedBundles,
        List<ImportedBundleReference> bundleRefs) => new()
        {
            Id = model.Id,
            Name = model.Name,
            OrganizationId = model.OrganizationId,
            Description = model.Description,
            IsSystemScenario = model.IsSystemScenario,
            ReportMethod = model.ReportMethod.ToString(),
            SelectedMeasures = model.SelectedMeasures.Select(m => m.ToString()).ToList(),
            Seed = model.Seed,
            PatientCount = model.PatientCount,
            ResourcesPerPatientMin = model.ResourcesPerPatientMin,
            ResourcesPerPatientMax = model.ResourcesPerPatientMax,
            PatientCohortsJson = JsonSerializer.Serialize(model.PatientCohorts),
            QueryPlanTemplateId = model.QueryPlanTemplateId,
            CleanupServiceData = model.CleanupServiceData,
            CleanupFhirData = model.CleanupFhirData,
            ReportPeriodStart = model.ReportPeriodStart?.UtcDateTime,
            ReportPeriodEnd = model.ReportPeriodEnd?.UtcDateTime,
            ImportedPatientIdsJson = JsonSerializer.Serialize(model.ImportedPatientIds),
            ImportedPatientBundlesJson = JsonSerializer.Serialize(sanitizedBundles),
            ImportedBundleRefs = bundleRefs,
            UpdatedAt = model.UpdatedAt
        };

    private static TestScenarioDefinition ToModel(
        TestScenarioDocument doc,
        IReadOnlyDictionary<Guid, string> bundlesById) => new()
        {
            Id = doc.Id,
            Name = doc.Name,
            OrganizationId = doc.OrganizationId,
            Description = doc.Description,
            IsSystemScenario = doc.IsSystemScenario,
            ReportMethod = Enum.TryParse<ReportMethod>(doc.ReportMethod, true, out var rm) ? rm : ReportMethod.Adhoc,
            SelectedMeasures = doc.SelectedMeasures
                .Select(s => Enum.TryParse<ProfiledMeasureType>(s, true, out var m) ? m : (ProfiledMeasureType?)null)
                .Where(m => m.HasValue)
                .Select(m => m!.Value)
                .ToList(),
            Seed = doc.Seed,
            PatientCount = doc.PatientCount,
            ResourcesPerPatientMin = doc.ResourcesPerPatientMin,
            ResourcesPerPatientMax = doc.ResourcesPerPatientMax,
            PatientCohorts = DeserializeCohorts(doc.PatientCohortsJson),
            QueryPlanTemplateId = doc.QueryPlanTemplateId,
            CleanupServiceData = doc.CleanupServiceData,
            CleanupFhirData = doc.CleanupFhirData,
            ReportPeriodStart = doc.ReportPeriodStart.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(doc.ReportPeriodStart.Value, DateTimeKind.Utc)) : null,
            ReportPeriodEnd = doc.ReportPeriodEnd.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(doc.ReportPeriodEnd.Value, DateTimeKind.Utc)) : null,
            ImportedPatientIds = DeserializeImported(doc.ImportedPatientIdsJson),
            ImportedPatientBundles = HydrateBundles(doc, bundlesById),
            UpdatedAt = doc.UpdatedAt
        };

    /// <summary>
    /// Restores each <see cref="ImportedPatientInput.BundleJson"/> from the externalized
    /// bundle collection. Falls back to whatever <c>BundleJson</c> is already on the
    /// deserialized input (legacy inline-embedded layout) when the document carries no
    /// references &mdash; this keeps pre-migration scenarios readable.
    /// </summary>
    private static List<ImportedPatientInput> HydrateBundles(
        TestScenarioDocument doc,
        IReadOnlyDictionary<Guid, string> bundlesById)
    {
        var inputs = DeserializeImported(doc.ImportedPatientBundlesJson);
        if (doc.ImportedBundleRefs.Count == 0 || inputs.Count == 0)
            return inputs;

        // Match refs to inputs by PatientId (the only stable key shared between the
        // two lists). Multiple bundles with the same PatientId are unusual but handled
        // by attaching the first available bundle and leaving subsequent ones empty.
        var refsByPatient = new Dictionary<string, Queue<Guid>>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in doc.ImportedBundleRefs)
        {
            var key = r.PatientId ?? string.Empty;
            if (!refsByPatient.TryGetValue(key, out var queue))
            {
                queue = new Queue<Guid>();
                refsByPatient[key] = queue;
            }
            queue.Enqueue(r.BundleId);
        }

        foreach (var input in inputs)
        {
            if (!string.IsNullOrEmpty(input.BundleJson))
                continue; // already inline (legacy doc) — leave as-is

            var key = input.PatientId ?? string.Empty;
            if (refsByPatient.TryGetValue(key, out var queue) && queue.Count > 0)
            {
                var bundleId = queue.Dequeue();
                input.UploadedBundleId = bundleId;
                if (bundlesById.TryGetValue(bundleId, out var json))
                    input.BundleJson = json;
            }
        }

        return inputs;
    }

    private static List<PatientCohortDefinition> DeserializeCohorts(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<PatientCohortDefinition>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static List<ImportedPatientInput> DeserializeImported(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<ImportedPatientInput>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
