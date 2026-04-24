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
        MigrateLegacyRunDateFields();
    }

    // —— Data migrations ——————————————————————————————————————————————

    /// <summary>
    /// Rewrites any automation_runs documents whose DateTimeOffset fields were persisted
    /// by the driver's default array-form serializer ([ticks, offsetMinutes]) into the
    /// current ISODate representation.
    ///
    /// Why this is necessary: BSON canonical type order places every Array value below
    /// every Date value, so server-side $gte filters on CreatedAt (used by the rolling
    /// 14-day dashboard query) would silently exclude legacy documents until they are
    /// next upserted. Rewriting them once at startup restores visibility and lets the
    /// idx_createdAt_desc index service the range scan.
    ///
    /// Idempotent: after the first successful run, the $type:"array" filter matches
    /// nothing and the method is a no-op.
    /// </summary>
    private void MigrateLegacyRunDateFields()
    {
        var typed = _database.GetCollection<AutomationRunDocument>("automation_runs");
        var raw = _database.GetCollection<BsonDocument>("automation_runs");

        // Match any run doc where at least one of the date fields is still an array.
        // $type with "array" is a BSON type alias supported by both Mongo and Cosmos.
        var legacyFilter = new BsonDocument
        {
            { "$or", new BsonArray
                {
                    new BsonDocument("CreatedAt",   new BsonDocument("$type", "array")),
                    new BsonDocument("StartedAt",   new BsonDocument("$type", "array")),
                    new BsonDocument("FinishedAt",  new BsonDocument("$type", "array")),
                    new BsonDocument("CompletedAt", new BsonDocument("$type", "array"))
                }
            }
        };

        int scanned = 0, rewritten = 0;

        try
        {
            // Use the raw collection to enumerate _id only; then reload each via the
            // typed collection so the driver's DateTimeOffsetSerializer performs the
            // legacy array -> DateTimeOffset conversion on the read path. Writing the
            // typed document back via ReplaceOne serializes every date field using the
            // now-configured BsonType.DateTime representation.
            var idProjection = Builders<BsonDocument>.Projection.Include("_id");
            using var cursor = raw.Find(legacyFilter).Project(idProjection).ToCursor();

            while (cursor.MoveNext())
            {
                foreach (var idDoc in cursor.Current)
                {
                    scanned++;

                    if (!idDoc.TryGetValue("_id", out var idValue))
                        continue;

                    var idFilter = new BsonDocument("_id", idValue);

                    try
                    {
                        var doc = typed.Find(idFilter).FirstOrDefault();
                        if (doc == null)
                            continue;

                        typed.ReplaceOne(idFilter, doc);
                        rewritten++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Failed to migrate legacy date fields for automation_runs document {Id}. " +
                            "The document will remain in array form and be invisible to dashboard range queries until re-upserted.",
                            idValue);
                    }
                }
            }

            if (rewritten > 0)
            {
                _logger.LogInformation(
                    "Migrated {Rewritten}/{Scanned} automation_runs document(s) from legacy array-form DateTimeOffset to ISODate representation.",
                    rewritten, scanned);
            }
            else
            {
                _logger.LogDebug("No automation_runs documents required date-field migration.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "automation_runs legacy date-field migration scan failed. Existing legacy documents may remain hidden from dashboard range queries.");
        }
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
