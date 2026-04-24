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
    /// Idempotent: after a successful run no documents match the <c>$type:"array"</c>
    /// probe, and the method is a no-op.
    /// </summary>
    private void MigrateLegacyRunDateFields()
    {
        var raw = _database.GetCollection<BsonDocument>("automation_runs");

        // Match any doc where at least one date field is still array-shaped.
        // $type with the string alias "array" is supported by MongoDB 3.2+ and the
        // Cosmos DB Mongo API, so we avoid the server-version-dependent numeric form.
        var legacyFilter = new BsonDocument("$or", new BsonArray
        {
            new BsonDocument("CreatedAt",   new BsonDocument("$type", "array")),
            new BsonDocument("StartedAt",   new BsonDocument("$type", "array")),
            new BsonDocument("FinishedAt",  new BsonDocument("$type", "array")),
            new BsonDocument("CompletedAt", new BsonDocument("$type", "array")),
        });

        var dateFields = new[] { "CreatedAt", "StartedAt", "FinishedAt", "CompletedAt" };

        int scanned = 0, rewritten = 0, failed = 0;

        try
        {
            using var cursor = raw.Find(legacyFilter).ToCursor();
            while (cursor.MoveNext())
            {
                foreach (var doc in cursor.Current)
                {
                    scanned++;

                    if (!doc.TryGetValue("_id", out var idValue))
                        continue;

                    var set = new BsonDocument();
                    foreach (var field in dateFields)
                    {
                        if (!doc.TryGetValue(field, out var value) || !value.IsBsonArray)
                            continue;

                        if (TryConvertLegacyDateArray(value.AsBsonArray, out var converted))
                            set[field] = converted;
                    }

                    if (set.ElementCount == 0)
                        continue;

                    try
                    {
                        raw.UpdateOne(
                            new BsonDocument("_id", idValue),
                            new BsonDocument("$set", set));
                        rewritten++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        _logger.LogWarning(ex,
                            "Failed to rewrite legacy date fields for automation_runs document {Id}. " +
                            "The document will remain in array form and stay hidden from dashboard range queries until re-upserted.",
                            idValue);
                    }
                }
            }

            if (scanned > 0)
            {
                // Logged at Information so operators can confirm the migration ran on
                // first startup after deploy; subsequent startups log nothing.
                _logger.LogInformation(
                    "automation_runs legacy date migration: scanned={Scanned}, rewritten={Rewritten}, failed={Failed}.",
                    scanned, rewritten, failed);
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

    /// <summary>
    /// Converts the driver's legacy two-element DateTimeOffset array
    /// (<c>[ticks, offsetMinutes]</c>) into a BSON <c>DateTime</c> normalized to UTC.
    /// </summary>
    private static bool TryConvertLegacyDateArray(BsonArray array, out BsonDateTime converted)
    {
        converted = default!;
        if (array.Count != 2)
            return false;

        try
        {
            var ticks = array[0].ToInt64();
            var offsetMinutes = array[1].ToInt32();
            var dto = new DateTimeOffset(ticks, TimeSpan.FromMinutes(offsetMinutes));
            converted = new BsonDateTime(dto.UtcDateTime);
            return true;
        }
        catch
        {
            return false;
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
