using Automation.UI.Models;
using LantanaGroup.Automation.Generation;
using MongoDB.Driver;

namespace Automation.UI.Services.Persistence;

/// <summary>
/// Cosmos DB for MongoDB RU compatible: Find / ReplaceOne-upsert / DeleteOne only.
/// No unique indexes (Cosmos unique indexes can only be created on empty collections
/// and cannot be modified). No aggregate pipelines. Bundle JSON is stored inline
/// (ACH files are well under the 2 MB Cosmos document limit).
/// </summary>
public sealed class MongoMeasureTemplateStore : IMeasureTemplateStore
{
    private readonly IMongoCollection<MeasureTemplateDocument> _collection;

    public MongoMeasureTemplateStore(IMongoDatabase database)
    {
        _collection = database.GetCollection<MeasureTemplateDocument>("automation_measure_templates");
    }

    public async Task<List<MeasureTemplate>> GetAllAsync(CancellationToken ct = default)
    {
        var docs = await _collection.Find(FilterDefinition<MeasureTemplateDocument>.Empty)
            .SortBy(d => d.Name)
            .ToListAsync(ct);

        return docs.Select(ToModel).ToList();
    }

    public async Task<MeasureTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var doc = await _collection.Find(d => d.Id == id).FirstOrDefaultAsync(ct);
        return doc == null ? null : ToModel(doc);
    }

    public async Task<List<MeasureTemplate>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
            return [];

        var docs = await _collection.Find(d => idList.Contains(d.Id)).ToListAsync(ct);
        var byId = docs.ToDictionary(d => d.Id);
        return idList.Where(byId.ContainsKey).Select(id => ToModel(byId[id])).ToList();
    }

    public async Task UpsertAsync(MeasureTemplate template, CancellationToken ct = default)
    {
        var doc = ToDocument(template);
        await _collection.ReplaceOneAsync(
            d => d.Id == doc.Id,
            doc,
            new ReplaceOptions { IsUpsert = true },
            ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _collection.DeleteOneAsync(d => d.Id == id, ct);
    }

    private static MeasureTemplateDocument ToDocument(MeasureTemplate model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        Description = model.Description,
        IsSystem = model.IsSystem,
        UpdatedAt = model.UpdatedAt,
        GenerationFamily = model.GenerationFamily.ToString(),
        BundleJson = model.BundleJson,
        MeasureId = model.MeasureId,
        CanonicalUrl = model.CanonicalUrl,
        Version = model.Version,
        MeasureDate = model.MeasureDate,
        Status = model.Status
    };

    private static MeasureTemplate ToModel(MeasureTemplateDocument doc) => new()
    {
        Id = doc.Id,
        Name = doc.Name,
        Description = doc.Description,
        IsSystem = doc.IsSystem,
        UpdatedAt = doc.UpdatedAt,
        GenerationFamily = Enum.TryParse<ProfiledMeasureType>(doc.GenerationFamily, true, out var family)
            ? family
            : ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation,
        BundleJson = doc.BundleJson,
        MeasureId = doc.MeasureId,
        CanonicalUrl = doc.CanonicalUrl,
        Version = doc.Version,
        MeasureDate = doc.MeasureDate,
        Status = doc.Status
    };
}
