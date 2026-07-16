using System.Text.Json;
using Automation.UI.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Automation.UI.Services.Persistence;

/// <summary>
/// MongoDB-backed implementation of <see cref="INormalizationStore"/>.
/// </summary>
public sealed class MongoNormalizationStore : INormalizationStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IMongoCollection<NormalizationOperationDocument> _operations;
    private readonly IMongoCollection<NormalizationSequenceDocument> _sequences;
    private readonly IMongoCollection<NormalizationSuiteDocument> _suites;

    public MongoNormalizationStore(IMongoDatabase database)
    {
        _operations = database.GetCollection<NormalizationOperationDocument>("automation_normalization_operations");
        _sequences = database.GetCollection<NormalizationSequenceDocument>("automation_normalization_sequences");
        _suites = database.GetCollection<NormalizationSuiteDocument>("automation_normalization_suites");
    }

    // ========== Operations ==========

    public async Task<List<NormalizationOperationDefinition>> GetAllOperationsAsync(CancellationToken ct = default)
    {
        var docs = await _operations.Find(FilterDefinition<NormalizationOperationDocument>.Empty)
            .SortBy(d => d.Name)
            .ToListAsync(ct);
        return docs.Select(ToModel).ToList();
    }

    public async Task<NormalizationOperationDefinition?> GetOperationByIdAsync(Guid id, CancellationToken ct = default)
    {
        var doc = await _operations.Find(d => d.Id == id).FirstOrDefaultAsync(ct);
        return doc == null ? null : ToModel(doc);
    }

    public async Task UpsertOperationAsync(NormalizationOperationDefinition op, CancellationToken ct = default)
    {
        var doc = ToDocument(op);
        await _operations.ReplaceOneAsync(d => d.Id == doc.Id, doc, new ReplaceOptions { IsUpsert = true }, ct);
    }

    public async Task DeleteOperationAsync(Guid id, CancellationToken ct = default)
    {
        await _operations.DeleteOneAsync(d => d.Id == id, ct);
    }

    // ========== Sequences ==========

    public async Task<List<NormalizationSequenceDefinition>> GetAllSequencesAsync(CancellationToken ct = default)
    {
        var docs = await _sequences.Find(FilterDefinition<NormalizationSequenceDocument>.Empty)
            .SortBy(d => d.Name)
            .ToListAsync(ct);
        return docs.Select(ToModel).ToList();
    }

    public async Task<NormalizationSequenceDefinition?> GetSequenceByIdAsync(Guid id, CancellationToken ct = default)
    {
        var doc = await _sequences.Find(d => d.Id == id).FirstOrDefaultAsync(ct);
        return doc == null ? null : ToModel(doc);
    }

    public async Task UpsertSequenceAsync(NormalizationSequenceDefinition seq, CancellationToken ct = default)
    {
        var doc = ToDocument(seq);
        await _sequences.ReplaceOneAsync(d => d.Id == doc.Id, doc, new ReplaceOptions { IsUpsert = true }, ct);
    }

    public async Task DeleteSequenceAsync(Guid id, CancellationToken ct = default)
    {
        await _sequences.DeleteOneAsync(d => d.Id == id, ct);
    }

    // ========== Suites ==========

    public async Task<List<NormalizationSuiteDefinition>> GetAllSuitesAsync(CancellationToken ct = default)
    {
        var docs = await _suites.Find(FilterDefinition<NormalizationSuiteDocument>.Empty)
            .SortBy(d => d.Name)
            .ToListAsync(ct);
        return docs.Select(ToModel).ToList();
    }

    public async Task<NormalizationSuiteDefinition?> GetSuiteByIdAsync(Guid id, CancellationToken ct = default)
    {
        var doc = await _suites.Find(d => d.Id == id).FirstOrDefaultAsync(ct);
        return doc == null ? null : ToModel(doc);
    }

    public async Task<NormalizationSuiteDefinition?> GetDefaultSuiteAsync(CancellationToken ct = default)
    {
        var doc = await _suites.Find(d => d.IsDefault).FirstOrDefaultAsync(ct);
        return doc == null ? null : ToModel(doc);
    }

    public async Task UpsertSuiteAsync(NormalizationSuiteDefinition suite, CancellationToken ct = default)
    {
        var doc = ToDocument(suite);
        await _suites.ReplaceOneAsync(d => d.Id == doc.Id, doc, new ReplaceOptions { IsUpsert = true }, ct);
    }

    public async Task SetDefaultSuiteAsync(Guid id, CancellationToken ct = default)
    {
        using var session = await _suites.Database.Client.StartSessionAsync(cancellationToken: ct);
        session.StartTransaction();
        try
        {
            var clearUpdate = Builders<NormalizationSuiteDocument>.Update.Set(d => d.IsDefault, false);
            await _suites.UpdateManyAsync(session, _ => true, clearUpdate, cancellationToken: ct);

            var setUpdate = Builders<NormalizationSuiteDocument>.Update.Set(d => d.IsDefault, true);
            await _suites.UpdateOneAsync(session, d => d.Id == id, setUpdate, cancellationToken: ct);

            await session.CommitTransactionAsync(ct);
        }
        catch
        {
            await session.AbortTransactionAsync(ct);
            throw;
        }
    }

    public async Task DeleteSuiteAsync(Guid id, CancellationToken ct = default)
    {
        await _suites.DeleteOneAsync(d => d.Id == id, ct);
    }

    // ========== Mapping Helpers ==========

    private static NormalizationOperationDocument ToDocument(NormalizationOperationDefinition m) => new()
    {
        Id = m.Id,
        Name = m.Name,
        Description = m.Description,
        OperationType = m.OperationType,
        ResourceTypes = m.ResourceTypes,
        IsSystem = m.IsSystem,
        UpdatedAt = m.UpdatedAt,
        ConfigJson = JsonSerializer.Serialize(new
        {
            m.SourceFhirPath,
            m.TargetFhirPath,
            m.ConditionTargetFhirPath,
            m.ConditionTargetValue,
            m.Conditions,
            m.CodeMapFhirPath,
            m.CodeSystemMaps,
            m.ExtensionUrls
        }, JsonOpts)
    };

    private static NormalizationOperationDefinition ToModel(NormalizationOperationDocument d)
    {
        var m = new NormalizationOperationDefinition
        {
            Id = d.Id,
            Name = d.Name,
            Description = d.Description,
            OperationType = d.OperationType,
            ResourceTypes = d.ResourceTypes ?? [],
            IsSystem = d.IsSystem,
            UpdatedAt = d.UpdatedAt
        };

        if (!string.IsNullOrWhiteSpace(d.ConfigJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(d.ConfigJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("sourceFhirPath", out var sfp) && sfp.ValueKind == JsonValueKind.String)
                    m.SourceFhirPath = sfp.GetString();
                if (root.TryGetProperty("targetFhirPath", out var tfp) && tfp.ValueKind == JsonValueKind.String)
                    m.TargetFhirPath = tfp.GetString();
                if (root.TryGetProperty("conditionTargetFhirPath", out var ctfp) && ctfp.ValueKind == JsonValueKind.String)
                    m.ConditionTargetFhirPath = ctfp.GetString();
                if (root.TryGetProperty("conditionTargetValue", out var ctv) && ctv.ValueKind != JsonValueKind.Null)
                    m.ConditionTargetValue = ctv.ValueKind == JsonValueKind.String ? ctv.GetString() : ctv.Clone();
                if (root.TryGetProperty("conditions", out var conds) && conds.ValueKind == JsonValueKind.Array)
                    m.Conditions = JsonSerializer.Deserialize<List<NormalizationCondition>>(conds.GetRawText(), JsonOpts) ?? [];
                if (root.TryGetProperty("codeMapFhirPath", out var cmfp) && cmfp.ValueKind == JsonValueKind.String)
                    m.CodeMapFhirPath = cmfp.GetString();
                if (root.TryGetProperty("codeSystemMaps", out var csm) && csm.ValueKind == JsonValueKind.Array)
                    m.CodeSystemMaps = JsonSerializer.Deserialize<List<NormalizationCodeSystemMap>>(csm.GetRawText(), JsonOpts) ?? [];
                if (root.TryGetProperty("extensionUrls", out var eu) && eu.ValueKind == JsonValueKind.Array)
                    m.ExtensionUrls = JsonSerializer.Deserialize<List<string>>(eu.GetRawText(), JsonOpts) ?? [];
            }
            catch { /* best effort */ }
        }

        return m;
    }

    private static NormalizationSequenceDocument ToDocument(NormalizationSequenceDefinition m) => new()
    {
        Id = m.Id,
        Name = m.Name,
        Description = m.Description,
        EntriesJson = JsonSerializer.Serialize(m.Entries, JsonOpts),
        IsSystem = m.IsSystem,
        UpdatedAt = m.UpdatedAt
    };

    private static NormalizationSequenceDefinition ToModel(NormalizationSequenceDocument d) => new()
    {
        Id = d.Id,
        Name = d.Name,
        Description = d.Description,
        Entries = Deserialize<List<NormalizationSequenceEntry>>(d.EntriesJson) ?? [],
        IsSystem = d.IsSystem,
        UpdatedAt = d.UpdatedAt
    };

    private static NormalizationSuiteDocument ToDocument(NormalizationSuiteDefinition m) => new()
    {
        Id = m.Id,
        Name = m.Name,
        Description = m.Description,
        OperationIds = m.OperationIds,
        SequenceIds = m.SequenceIds,
        IsSystem = m.IsSystem,
        IsDefault = m.IsDefault,
        UpdatedAt = m.UpdatedAt
    };

    private static NormalizationSuiteDefinition ToModel(NormalizationSuiteDocument d) => new()
    {
        Id = d.Id,
        Name = d.Name,
        Description = d.Description,
        OperationIds = d.OperationIds ?? [],
        SequenceIds = d.SequenceIds ?? [],
        IsSystem = d.IsSystem,
        IsDefault = d.IsDefault,
        UpdatedAt = d.UpdatedAt
    };

    private static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json, JsonOpts); }
        catch { return default; }
    }
}

// ========== MongoDB Document Classes ==========

internal class NormalizationOperationDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public List<string> ResourceTypes { get; set; } = [];
    public bool IsSystem { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string ConfigJson { get; set; } = "{}";
}

internal class NormalizationSequenceDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EntriesJson { get; set; } = "[]";
    public bool IsSystem { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

internal class NormalizationSuiteDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    [BsonSerializer(typeof(GuidStringListSerializer))]
    public List<Guid> OperationIds { get; set; } = [];
    [BsonSerializer(typeof(GuidStringListSerializer))]
    public List<Guid> SequenceIds { get; set; } = [];
    public bool IsSystem { get; set; }
    public bool IsDefault { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class GuidStringListSerializer : SerializerBase<List<Guid>>
{
    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, List<Guid> value)
    {
        if (value == null)
        {
            context.Writer.WriteNull();
            return;
        }

        context.Writer.WriteStartArray();
        foreach (var guid in value)
            context.Writer.WriteString(guid.ToString());
        context.Writer.WriteEndArray();
    }

    public override List<Guid> Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        if (context.Reader.GetCurrentBsonType() == BsonType.Null)
        {
            context.Reader.ReadNull();
            return [];
        }

        var result = new List<Guid>();
        context.Reader.ReadStartArray();
        while (context.Reader.ReadBsonType() != BsonType.EndOfDocument)
        {
            if (context.Reader.GetCurrentBsonType() != BsonType.String)
                throw new FormatException("Expected GUID array values to be stored as BSON string.");

            var raw = context.Reader.ReadString();
            if (!Guid.TryParse(raw, out var parsed))
                throw new FormatException($"Invalid GUID value '{raw}' in BSON string array.");

            result.Add(parsed);
        }
        context.Reader.ReadEndArray();
        return result;
    }
}
