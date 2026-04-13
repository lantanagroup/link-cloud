using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.Extensions.Logging;

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
        EnsureSnapshotIndexes();
        EnsureScenarioIndexes();
        EnsureQueryPlanTemplateIndexes();
    }

    // ?? automation_runs ??????????????????????????????????????????????

    private void EnsureRunIndexes()
    {
        var collection = _database.GetCollection<BsonDocument>("automation_runs");

        // Sort index for GetRunsPageAsync (ORDER BY CreatedAt DESC).
        CreateIndexSafe(collection, new BsonDocument { { "CreatedAt", -1 } }, unique: false, "idx_createdAt_desc");

        // Filter index for GetActiveRunsAsync (WHERE IsActive = true).
        CreateIndexSafe(collection, new BsonDocument { { "IsActive", 1 } }, unique: false, "idx_isActive");
    }

    // ?? automation_snapshots ?????????????????????????????????????????

    private void EnsureSnapshotIndexes()
    {
        var collection = _database.GetCollection<BsonDocument>("automation_snapshots");

        // Compound key used for upserts and lookups (RunId + Domain).
        // Unique constraint enforces one snapshot per run+domain pair.
        CreateIndexSafe(collection, new BsonDocument { { "RunId", 1 }, { "Domain", 1 } }, unique: true, "idx_runId_domain_unique");
    }

    // ?? automation_scenarios ?????????????????????????????????????????

    private void EnsureScenarioIndexes()
    {
        var collection = _database.GetCollection<BsonDocument>("automation_scenarios");

        // Sort index for GetAllAsync (ORDER BY Name ASC).
        CreateIndexSafe(collection, new BsonDocument { { "Name", 1 } }, unique: false, "idx_name_asc");
    }

    // ?? automation_query_plan_templates ???????????????????????????????

    private void EnsureQueryPlanTemplateIndexes()
    {
        var collection = _database.GetCollection<BsonDocument>("automation_query_plan_templates");

        // Sort index for GetAllAsync (ORDER BY Name ASC).
        CreateIndexSafe(collection, new BsonDocument { { "Name", 1 } }, unique: false, "idx_name_asc");
    }

    // ?? Helpers ??????????????????????????????????????????????????????

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
