using System.Text.Json;
using System.Text.Json.Serialization;
using LantanaGroup.Automation.Generation;
using MongoDB.Driver;

namespace Automation.UI.Services.Persistence;

public sealed class MongoGenerationCatalogStore : IGenerationCatalogStore
{
    public const string CollectionName = "automation_generation_catalog";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IMongoCollection<GenerationCatalogDocument> _collection;

    public MongoGenerationCatalogStore(IMongoDatabase database)
    {
        _collection = database.GetCollection<GenerationCatalogDocument>(CollectionName);
    }

    public async Task<List<GenerationCatalogItem>> GetAllAsync(CancellationToken ct = default)
    {
        var docs = await _collection.Find(FilterDefinition<GenerationCatalogDocument>.Empty).ToListAsync(ct);
        return docs.Select(ToModel).Where(m => m != null).Cast<GenerationCatalogItem>().ToList();
    }

    public async Task MergeAsync(IEnumerable<GenerationCatalogItem> items, CancellationToken ct = default)
    {
        var incomingList = items.Where(i => !string.IsNullOrWhiteSpace(i.Code)).ToList();
        if (incomingList.Count == 0)
            return;

        var existing = await _collection.Find(FilterDefinition<GenerationCatalogDocument>.Empty).ToListAsync(ct);
        var map = new Dictionary<string, GenerationCatalogDocument>(StringComparer.OrdinalIgnoreCase);
        foreach (var doc in existing)
            map[Key(doc.Kind, doc.System, doc.Code)] = doc;

        var writes = new List<WriteModel<GenerationCatalogDocument>>();
        foreach (var incoming in incomingList)
        {
            incoming.System = GenerationCatalogItem.GuessSystem(incoming.Kind, incoming.System, incoming.Code);
            var key = Key(incoming.Kind.ToString(), incoming.System, incoming.Code);
            if (!map.TryGetValue(key, out var existingDoc))
            {
                incoming.Id = incoming.Id == Guid.Empty ? Guid.NewGuid() : incoming.Id;
                incoming.UpdatedAt = DateTimeOffset.UtcNow;
                var inserted = ToDocument(incoming);
                map[key] = inserted;
                writes.Add(new InsertOneModel<GenerationCatalogDocument>(inserted));
                continue;
            }

            var model = ToModel(existingDoc) ?? incoming;
            if (string.IsNullOrWhiteSpace(model.Display))
                model.Display = incoming.Display;
            model.Category ??= incoming.Category;
            model.Unit ??= incoming.Unit;
            model.NormLow ??= incoming.NormLow;
            model.NormHigh ??= incoming.NormHigh;
            model.IcdCode ??= incoming.IcdCode;
            if (!model.IsLab)
                model.IsLab = incoming.IsLab;
            model.Incomplete = string.IsNullOrWhiteSpace(model.Unit)
                && model.Kind == GenerationCatalogKind.Observation;
            model.SourceValueSet ??= incoming.SourceValueSet;
            model.SourceMeasure ??= incoming.SourceMeasure;
            if (incoming.IsSeed)
                model.IsSeed = true;

            var merged = ToDocument(model);
            if (string.Equals(existingDoc.PayloadJson, merged.PayloadJson, StringComparison.Ordinal)
                && string.Equals(existingDoc.Display, merged.Display, StringComparison.Ordinal)
                && existingDoc.Incomplete == merged.Incomplete
                && string.Equals(existingDoc.SourceValueSet, merged.SourceValueSet, StringComparison.Ordinal))
            {
                continue;
            }

            model.UpdatedAt = DateTimeOffset.UtcNow;
            merged = ToDocument(model);
            map[key] = merged;
            writes.Add(new ReplaceOneModel<GenerationCatalogDocument>(
                Builders<GenerationCatalogDocument>.Filter.Eq(d => d.Id, existingDoc.Id),
                merged));
        }

        const int chunk = 50;
        for (var i = 0; i < writes.Count; i += chunk)
        {
            var slice = writes.Skip(i).Take(chunk).ToList();
            await _collection.BulkWriteAsync(slice, new BulkWriteOptions { IsOrdered = false }, ct);
        }
    }

    private static string Key(string kind, string system, string code)
        => $"{kind}|{system}|{code}";

    private static GenerationCatalogDocument ToDocument(GenerationCatalogItem model) => new()
    {
        Id = model.Id,
        Kind = model.Kind.ToString(),
        System = model.System,
        Code = model.Code,
        Display = model.Display,
        Incomplete = model.Incomplete,
        IsSeed = model.IsSeed,
        SourceValueSet = model.SourceValueSet,
        UpdatedAt = model.UpdatedAt,
        PayloadJson = JsonSerializer.Serialize(model, JsonOpts)
    };

    private static GenerationCatalogItem? ToModel(GenerationCatalogDocument doc)
    {
        GenerationCatalogItem? model = null;
        if (!string.IsNullOrWhiteSpace(doc.PayloadJson))
        {
            try { model = JsonSerializer.Deserialize<GenerationCatalogItem>(doc.PayloadJson, JsonOpts); }
            catch { model = null; }
        }

        model ??= new GenerationCatalogItem();
        model.Id = doc.Id;
        if (Enum.TryParse<GenerationCatalogKind>(doc.Kind, out var kind))
            model.Kind = kind;
        model.System = doc.System;
        model.Code = doc.Code;
        if (string.IsNullOrWhiteSpace(model.Display))
            model.Display = doc.Display;
        return model;
    }
}
