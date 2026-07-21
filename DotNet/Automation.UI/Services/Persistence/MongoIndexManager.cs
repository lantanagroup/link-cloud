using MongoDB.Bson;
using MongoDB.Driver;

namespace Automation.UI.Services.Persistence;

/// <summary>
/// Centralises MongoDB index creation for all Automation.UI collections.
/// Compatible with both native MongoDB and Cosmos DB for MongoDB API.
///
/// Strategy:
///   1. List existing indexes and compare key shapes.
///   2. Skip creation when an index with the same keys already exists
///      (avoids Cosmos "unique index cannot be modified" errors).
///   3. Swallow and log any creation failure so the app still starts.
/// </summary>
public sealed class MongoIndexManager
{
    private readonly IMongoDatabase _database;
    private readonly ILogger<MongoIndexManager> _logger;

    public MongoIndexManager(IMongoDatabase database, ILogger<MongoIndexManager> logger)
    {
        _database = database;
        _logger = logger;
    }

    /// <summary>
    /// Ensures all indexes required by the Automation.UI stores exist.
    /// Safe to call on every startup — idempotent.
    /// </summary>
    public void EnsureAllIndexes()
    {
        EnsureRunIndexes();
        EnsureRunInputIndexes();
        EnsureSnapshotIndexes();
        EnsureScenarioIndexes();
        EnsureImportedBundleIndexes();
        EnsureQueryPlanTemplateIndexes();
        EnsureNormalizationIndexes();
        EnsureOrganizationResourceMapTemplateIndexes();
        EnsureApiHealthRunIndexes();
        EnsureApiHealthExecutionRunIndexes();
    }

    // --- automation_org_resource_map_templates ---

    private void EnsureOrganizationResourceMapTemplateIndexes()
    {
        var collection = _database.GetCollection<BsonDocument>("automation_org_resource_map_templates");
        CreateIndexSafe(collection, new BsonDocument { { "Name", 1 } }, unique: false, "idx_name_asc");
        CreateIndexSafe(collection, new BsonDocument { { "IsDefault", 1 } }, unique: false, "idx_isDefault");
    }

    // --- automation_runs ---

    private void EnsureRunIndexes()
    {
        var collection = _database.GetCollection<BsonDocument>("automation_runs");

        // Sort index for GetRunsPageAsync (default ORDER BY CreatedAt DESC).
        CreateIndexSafe(collection, new BsonDocument { { "CreatedAt", -1 } }, unique: false, "idx_createdAt_desc");

        // Filter index for GetActiveRunsAsync (WHERE IsActive = true).
        CreateIndexSafe(collection, new BsonDocument { { "IsActive", 1 } }, unique: false, "idx_isActive");

        // Single-field sort indexes for the user-selectable columns exposed by
        // the Recent Runs table on the dashboard. Without these, picking any
        // sort other than the default CreatedAt forces Cosmos / Mongo to do a
        // full collection scan + in-memory sort, which becomes expensive once
        // automation_runs grows past a few thousand documents.
        //
        // Direction is intentionally ascending (1). Both Cosmos Mongo API and
        // native MongoDB can scan a single-field index in reverse, so one
        // index serves both ASC and DESC for the same column — no need for a
        // mirrored "_desc" copy.
        //
        // BSON field names match the C# property casing (PascalCase) because
        // no camelCase convention is registered on the Mongo client; see
        // MongoDocuments.AutomationRunDocument. Lowercase variants would
        // create dead indexes that the query planner never picks up.
        //
        // Cosmos DB (RU model) note: every secondary index here costs write
        // RU/s on each UpsertRunSummaryAsync call. We deliberately keep the
        // set minimal:
        //   - Only single-field indexes, no {Sort, RunId} compound matrix.
        //     Cosmos DB for MongoDB API rejects multi-field ORDER BY queries
        //     without a matching composite index outright ("The order by
        //     query does not have a corresponding composite index that it
        //     can be served from"), unlike native MongoDB which silently
        //     applies an in-memory secondary sort. To stay index-resident on
        //     Cosmos without doubling the index count, MongoSnapshotStore
        //     .GetRunsPageAsync issues a single-field server-side sort and
        //     applies the RunId tiebreaker client-side after the page is
        //     fetched (within-page determinism only).
        //   - No indexes on derived/string-formatted columns like Duration
        //     (intentionally not sortable in the UI).
        CreateIndexSafe(collection, new BsonDocument { { "RunName",      1 } }, unique: false, "idx_runName_asc");
        CreateIndexSafe(collection, new BsonDocument { { "PatientCount", 1 } }, unique: false, "idx_patientCount_asc");
        CreateIndexSafe(collection, new BsonDocument { { "Seed",         1 } }, unique: false, "idx_seed_asc");
        CreateIndexSafe(collection, new BsonDocument { { "Status",       1 } }, unique: false, "idx_status_asc");
        CreateIndexSafe(collection, new BsonDocument { { "FinishedAt",   1 } }, unique: false, "idx_finishedAt_asc");
    }

    private void EnsureRunInputIndexes()
    {
        var collection = _database.GetCollection<BsonDocument>("automation_run_inputs");

        CreateIndexSafe(collection, new BsonDocument { { "UpdatedAt", -1 } }, unique: false, "idx_updatedAt_desc");
    }

    // --- automation_snapshots ---

    private void EnsureSnapshotIndexes()
    {
        var collection = _database.GetCollection<BsonDocument>("automation_snapshots");

        // Compound key used for upserts and lookups (RunId + Domain)
        CreateIndexSafe(collection, new BsonDocument { { "RunId", 1 }, { "Domain", 1 } }, unique: false, "idx_runId_domain");
    }

    // --- automation_scenarios ---

    private void EnsureScenarioIndexes()
    {
        var collection = _database.GetCollection<BsonDocument>("automation_scenarios");

        // Sort index for GetAllAsync (ORDER BY Name ASC).
        CreateIndexSafe(collection, new BsonDocument { { "Name", 1 } }, unique: false, "idx_name_asc");
    }

    // --- automation_imported_bundles ---

    private void EnsureImportedBundleIndexes()
    {
        var collection = _database.GetCollection<BsonDocument>("automation_imported_bundles");

        // Unique index on the SHA-256 ContentHash powers the cross-scenario "find or
        // insert" upsert in MongoScenarioStore.ResolveOrInsertBundleRefsAsync &mdash;
        // it guarantees identical bundle JSON resolves to a single document even
        // under concurrent saves. CreateIndexSafe gracefully handles Cosmos DB's
        // restriction on modifying unique indexes after collection creation.
        CreateIndexSafe(collection, new BsonDocument { { "ContentHash", 1 } }, unique: true, "idx_contentHash_unique");

        // Multikey index on ScenarioIds backs the orphan-prune path
        // (DetachAndPruneOrphansAsync): "find every bundle that references this scenario"
        // would otherwise scan the entire collection on every save and delete.
        CreateIndexSafe(collection, new BsonDocument { { "ScenarioIds", 1 } }, unique: false, "idx_scenarioIds");
    }

    // --- automation_query_plan_templates ---

    private void EnsureQueryPlanTemplateIndexes()
    {
        var collection = _database.GetCollection<BsonDocument>("automation_query_plan_templates");

        // Sort index for GetAllAsync (ORDER BY Name ASC).
        CreateIndexSafe(collection, new BsonDocument { { "Name", 1 } }, unique: false, "idx_name_asc");
    }

    // --- automation_normalization_* ---

    private void EnsureNormalizationIndexes()
    {
        var operations = _database.GetCollection<BsonDocument>("automation_normalization_operations");
        CreateIndexSafe(operations, new BsonDocument { { "Name", 1 } }, unique: false, "idx_name_asc");

        var sequences = _database.GetCollection<BsonDocument>("automation_normalization_sequences");
        CreateIndexSafe(sequences, new BsonDocument { { "Name", 1 } }, unique: false, "idx_name_asc");

        var suites = _database.GetCollection<BsonDocument>("automation_normalization_suites");
        CreateIndexSafe(suites, new BsonDocument { { "Name", 1 } }, unique: false, "idx_name_asc");
        CreateIndexSafe(suites, new BsonDocument { { "IsDefault", 1 } }, unique: false, "idx_isDefault");
    }

    // --- api_health_runs ---

    private void EnsureApiHealthRunIndexes()
    {
        var collection = _database.GetCollection<BsonDocument>("api_health_runs");

        CreateIndexSafe(collection, new BsonDocument { { "RunId", 1 }, { "ServiceName", 1 } }, unique: false, "idx_runId_serviceName");
        CreateIndexSafe(collection, new BsonDocument { { "StartedAt", -1 } }, unique: false, "idx_startedAt_desc");
        CreateIndexSafe(collection, new BsonDocument { { "ServiceName", 1 }, { "StartedAt", -1 } }, unique: false, "idx_serviceName_startedAt");
        CreateIndexSafe(collection, new BsonDocument { { "EndpointResults.EndpointKey", 1 }, { "StartedAt", -1 } }, unique: false, "idx_endpoint_results_endpointKey_startedAt");
    }

    // --- api_health_execution_runs ---

    private void EnsureApiHealthExecutionRunIndexes()
    {
        var collection = _database.GetCollection<BsonDocument>("api_health_execution_runs");

        CreateIndexSafe(collection, new BsonDocument { { "IsCompleted", 1 }, { "StartedAt", -1 } }, unique: false, "idx_isCompleted_startedAt");
    }

    // --- Helpers ---

    private void CreateIndexSafe(IMongoCollection<BsonDocument> collection, BsonDocument keys, bool unique, string name)
    {
        try
        {
            if (HasIndexWithKeys(collection, keys))
            {
                _logger.LogDebug("Index {IndexName} already exists on {Collection} — skipping.", name, collection.CollectionNamespace.CollectionName);
                return;
            }

            var options = new CreateIndexOptions { Name = name, Unique = unique };
            var model = new CreateIndexModel<BsonDocument>(new BsonDocumentIndexKeysDefinition<BsonDocument>(keys), options);
            collection.Indexes.CreateOne(model);

            _logger.LogInformation("Created index {IndexName} on {Collection}.", name, collection.CollectionNamespace.CollectionName);
        }
        catch (MongoCommandException ex) when (ex.Code == 13 && ex.Message.Contains("unique index cannot be modified", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "Skipping index {IndexName} on {Collection}: Cosmos rejected unique index modification. Existing collection/index policy will be used.",
                name,
                collection.CollectionNamespace.CollectionName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to create index {IndexName} on {Collection} - queries using this index may fall back to client-side evaluation.",
                name, collection.CollectionNamespace.CollectionName);
        }
    }

    private static bool HasIndexWithKeys(IMongoCollection<BsonDocument> collection, BsonDocument targetKeys)
    {
        var indexes = collection.Indexes.List().ToList();

        foreach (var index in indexes)
        {
            if (index.TryGetValue("key", out var keyValue)
                && keyValue.IsBsonDocument
                && KeysEqual(keyValue.AsBsonDocument, targetKeys))
            {
                return true;
            }
        }

        return false;
    }

    private static bool KeysEqual(BsonDocument existing, BsonDocument target)
    {
        if (existing.ElementCount != target.ElementCount)
            return false;

        var existingElements = existing.Elements.ToList();
        var targetElements = target.Elements.ToList();

        for (var i = 0; i < existingElements.Count; i++)
        {
            var left = existingElements[i];
            var right = targetElements[i];

            if (!string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase))
                return false;

            if (left.Value.ToInt32() != right.Value.ToInt32())
                return false;
        }

        return true;
    }
}
