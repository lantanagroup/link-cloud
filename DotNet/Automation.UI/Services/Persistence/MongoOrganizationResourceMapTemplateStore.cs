using System.Text.Json;
using Automation.UI.Models;
using MongoDB.Driver;

namespace Automation.UI.Services.Persistence;

public sealed class MongoOrganizationResourceMapTemplateStore : IOrganizationResourceMapTemplateStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IMongoCollection<OrganizationResourceMapTemplateDocument> _collection;

    public MongoOrganizationResourceMapTemplateStore(IMongoDatabase database)
    {
        _collection = database.GetCollection<OrganizationResourceMapTemplateDocument>("automation_org_resource_map_templates");
    }

    public async Task<List<OrganizationResourceMapTemplate>> GetAllAsync(CancellationToken ct = default)
    {
        var docs = await _collection.Find(FilterDefinition<OrganizationResourceMapTemplateDocument>.Empty)
            .SortBy(d => d.Name)
            .ToListAsync(ct);
        return docs.Select(ToModel).ToList();
    }

    public async Task<OrganizationResourceMapTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var doc = await _collection.Find(d => d.Id == id).FirstOrDefaultAsync(ct);
        return doc == null ? null : ToModel(doc);
    }

    public async Task<OrganizationResourceMapTemplate?> GetDefaultAsync(CancellationToken ct = default)
    {
        var doc = await _collection.Find(d => d.IsDefault).FirstOrDefaultAsync(ct);
        return doc == null ? null : ToModel(doc);
    }

    public async Task UpsertAsync(OrganizationResourceMapTemplate template, CancellationToken ct = default)
    {
        var doc = ToDocument(template);
        await _collection.ReplaceOneAsync(d => d.Id == doc.Id, doc, new ReplaceOptions { IsUpsert = true }, ct);
    }

    public async Task SetDefaultAsync(Guid id, CancellationToken ct = default)
    {
        using var session = await _collection.Database.Client.StartSessionAsync(cancellationToken: ct);
        session.StartTransaction();
        try
        {
            await _collection.UpdateManyAsync(session, _ => true,
                Builders<OrganizationResourceMapTemplateDocument>.Update.Set(d => d.IsDefault, false),
                cancellationToken: ct);

            await _collection.UpdateOneAsync(session, d => d.Id == id,
                Builders<OrganizationResourceMapTemplateDocument>.Update.Set(d => d.IsDefault, true),
                cancellationToken: ct);

            await session.CommitTransactionAsync(ct);
        }
        catch
        {
            await session.AbortTransactionAsync(ct);
            throw;
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _collection.DeleteOneAsync(d => d.Id == id, ct);
    }

    private static OrganizationResourceMapTemplateDocument ToDocument(OrganizationResourceMapTemplate model) => new()
    {
        Id = model.Id,
        Name = model.Name.Trim(),
        NormalizedName = NormalizeName(model.Name),
        Description = model.Description,
        IsSystem = model.IsSystem,
        IsDefault = model.IsDefault,
        ConditionsJson = JsonSerializer.Serialize(model.Conditions, JsonOpts),
        UpdatedAt = model.UpdatedAt
    };

    private static OrganizationResourceMapTemplate ToModel(OrganizationResourceMapTemplateDocument doc) => new()
    {
        Id = doc.Id,
        Name = doc.Name,
        NormalizedName = doc.NormalizedName,
        Description = doc.Description,
        IsSystem = doc.IsSystem,
        IsDefault = doc.IsDefault,
        Conditions = Deserialize<List<OrganizationResourceMapCondition>>(
        doc.ConditionsJson) ?? [],
        UpdatedAt = doc.UpdatedAt
    };

    private static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json, JsonOpts); }
        catch { return default; }
    }

    private static string NormalizeName(string name)
    {
        return name.Trim().ToUpperInvariant();
    }
}
