using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Security.Cryptography;
using System.Text;

namespace Automation.UI.Services.Persistence;

[BsonIgnoreExtraElements]
public sealed class GeneratedTemplateCacheVersionDocument
{
    [BsonId]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public Guid Id { get; set; }

    public string ScenarioKey { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public string TemplateSetHash { get; set; } = string.Empty;
    public List<string> TemplateKeys { get; set; } = [];

    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public Guid CreatedByRunId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastUsedAt { get; set; }
}

public sealed record GeneratedTemplateCacheVersionBinding(Guid VersionId, int VersionNumber, string ScenarioKey, string TemplateSetHash);

public sealed class GeneratedTemplateCacheVersionStore
{
    private readonly IMongoCollection<GeneratedTemplateCacheVersionDocument> _versions;
    private const string ScenarioHashUniqueIndexName = "ux_generated_template_versions_scenario_hash";

    public GeneratedTemplateCacheVersionStore(IMongoDatabase database)
    {
        _versions = database.GetCollection<GeneratedTemplateCacheVersionDocument>("automation_generated_template_versions");

        if (HasIndexWithKeys(_versions, new BsonDocument { { "ScenarioKey", 1 }, { "TemplateSetHash", 1 } }))
            return;

        var uniqueScenarioHashIndex = new CreateIndexModel<GeneratedTemplateCacheVersionDocument>(
            Builders<GeneratedTemplateCacheVersionDocument>.IndexKeys
                .Ascending(version => version.ScenarioKey)
                .Ascending(version => version.TemplateSetHash),
            new CreateIndexOptions { Unique = true, Name = ScenarioHashUniqueIndexName });

        try
        {
            _versions.Indexes.CreateOne(uniqueScenarioHashIndex);
        }
        catch (MongoCommandException ex)
            when (ex.Message.Contains("already exists with different options", StringComparison.OrdinalIgnoreCase)
               || ex.Message.Contains("Cannot create unique index when collection contains documents", StringComparison.OrdinalIgnoreCase)
               || (ex.Code == 13 && ex.Message.Contains("unique index cannot be modified", StringComparison.OrdinalIgnoreCase)))
        {
            // Deployed environments may already have an index with this name/options
            // drift or contain pre-existing duplicate rows. Do not block app startup.
        }
    }

    private static bool HasIndexWithKeys(
        IMongoCollection<GeneratedTemplateCacheVersionDocument> collection,
        BsonDocument targetKeys)
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

    public async Task<GeneratedTemplateCacheVersionBinding?> BindRunAsync(
        Guid runId,
        Guid? scenarioId,
        string? scenarioName,
        IReadOnlyList<string> templateKeys,
        CancellationToken ct = default)
    {
        if (templateKeys.Count == 0)
            return null;

        var ordered = templateKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();
        if (ordered.Count == 0)
            return null;

        var scenarioKey = BuildScenarioKey(scenarioId, scenarioName, ordered);
        var hash = ComputeTemplateSetHash(ordered);

        var existing = await _versions
            .Find(version => version.ScenarioKey == scenarioKey && version.TemplateSetHash == hash)
            .SortByDescending(version => version.VersionNumber)
            .FirstOrDefaultAsync(ct);

        if (existing != null)
        {
            await _versions.UpdateOneAsync(
                version => version.Id == existing.Id,
                Builders<GeneratedTemplateCacheVersionDocument>.Update.Set(version => version.LastUsedAt, DateTimeOffset.UtcNow),
                cancellationToken: ct);

            return new GeneratedTemplateCacheVersionBinding(existing.Id, existing.VersionNumber, existing.ScenarioKey, existing.TemplateSetHash);
        }

        var latest = await _versions
            .Find(version => version.ScenarioKey == scenarioKey)
            .SortByDescending(version => version.VersionNumber)
            .FirstOrDefaultAsync(ct);

        var versionNumber = (latest?.VersionNumber ?? 0) + 1;
        var now = DateTimeOffset.UtcNow;
        var doc = new GeneratedTemplateCacheVersionDocument
        {
            Id = Guid.NewGuid(),
            ScenarioKey = scenarioKey,
            VersionNumber = versionNumber,
            TemplateSetHash = hash,
            TemplateKeys = ordered,
            CreatedByRunId = runId,
            CreatedAt = now,
            LastUsedAt = now
        };

        try
        {
            await _versions.InsertOneAsync(doc, cancellationToken: ct);
            return new GeneratedTemplateCacheVersionBinding(doc.Id, doc.VersionNumber, doc.ScenarioKey, doc.TemplateSetHash);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var existingAfterRace = await _versions
                .Find(version => version.ScenarioKey == scenarioKey && version.TemplateSetHash == hash)
                .SortByDescending(version => version.VersionNumber)
                .FirstOrDefaultAsync(ct);

            if (existingAfterRace == null)
                throw;

            await _versions.UpdateOneAsync(
                version => version.Id == existingAfterRace.Id,
                Builders<GeneratedTemplateCacheVersionDocument>.Update.Set(version => version.LastUsedAt, DateTimeOffset.UtcNow),
                cancellationToken: ct);

            return new GeneratedTemplateCacheVersionBinding(
                existingAfterRace.Id,
                existingAfterRace.VersionNumber,
                existingAfterRace.ScenarioKey,
                existingAfterRace.TemplateSetHash);
        }
    }

    private static string BuildScenarioKey(Guid? scenarioId, string? scenarioName, IReadOnlyList<string> templateKeys)
    {
        if (scenarioId.HasValue)
            return $"scenario:{scenarioId.Value:D}";

        if (!string.IsNullOrWhiteSpace(scenarioName))
            return $"name:{scenarioName.Trim()}";

        return $"adhoc:{ComputeTemplateSetHash(templateKeys)}";
    }

    private static string ComputeTemplateSetHash(IReadOnlyList<string> templateKeys)
    {
        var payload = string.Join('|', templateKeys);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}