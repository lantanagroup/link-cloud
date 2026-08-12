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
    private const string ScenarioKeyField = "ScenarioKey";
    private const string TemplateSetHashField = "TemplateSetHash";

    public GeneratedTemplateCacheVersionStore(IMongoDatabase database)
    {
        _versions = database.GetCollection<GeneratedTemplateCacheVersionDocument>("automation_generated_template_versions");

        var indexes = _versions.Indexes.List().ToList();
        var indexInvariant = GetScenarioHashIndexInvariant(indexes);
        if (indexInvariant == ScenarioHashIndexInvariant.Satisfied)
            return;

        if (indexInvariant == ScenarioHashIndexInvariant.NonUniqueKeyShapePresent)
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
        catch
        {
            // Best-effort only: startup must not be blocked by index creation limitations
            // in deployed environments (e.g., Cosmos DB collections with existing data).
        }
    }

    private static ScenarioHashIndexInvariant GetScenarioHashIndexInvariant(IReadOnlyList<BsonDocument> indexes)
    {
        foreach (var index in indexes)
        {
            if (index.TryGetValue("key", out var keyValue)
                && keyValue.IsBsonDocument
                && IsScenarioHashKeyShape(keyValue.AsBsonDocument))
            {
                var unique = index.TryGetValue("unique", out var uniqueValue)
                    && uniqueValue.IsBoolean
                    && uniqueValue.AsBoolean;

                return unique
                    ? ScenarioHashIndexInvariant.Satisfied
                    : ScenarioHashIndexInvariant.NonUniqueKeyShapePresent;
            }
        }

        return ScenarioHashIndexInvariant.Missing;
    }

    private static bool IsScenarioHashKeyShape(BsonDocument key)
    {
        if (key.ElementCount != 2)
            return false;

        var elements = key.Elements.ToList();

        if (!string.Equals(elements[0].Name, ScenarioKeyField, StringComparison.Ordinal))
            return false;
        if (!string.Equals(elements[1].Name, TemplateSetHashField, StringComparison.Ordinal))
            return false;

        return IsAscendingDirection(elements[0].Value)
            && IsAscendingDirection(elements[1].Value);
    }

    private static bool IsAscendingDirection(BsonValue value)
    {
        if (!TryGetIntegralDirection(value, out var direction))
            return false;

        return direction == 1;
    }

    private static bool TryGetIntegralDirection(BsonValue value, out int direction)
    {
        direction = 0;

        if (value is BsonInt32 int32)
        {
            direction = int32.Value;
            return true;
        }

        if (value is BsonInt64 int64)
        {
            if (int64.Value is < int.MinValue or > int.MaxValue)
                return false;

            direction = (int)int64.Value;
            return true;
        }

        return false;
    }

    private enum ScenarioHashIndexInvariant
    {
        Missing,
        Satisfied,
        NonUniqueKeyShapePresent
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