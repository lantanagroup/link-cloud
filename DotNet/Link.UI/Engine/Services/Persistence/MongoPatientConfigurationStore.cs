using System.Text.Json;
using System.Text.Json.Serialization;
using Automation.UI.Models;
using LantanaGroup.Automation.Generation;
using MongoDB.Driver;

namespace Automation.UI.Services.Persistence;

public sealed class MongoPatientConfigurationStore : IPatientConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IMongoCollection<PatientConfigurationDocument> _collection;

    public MongoPatientConfigurationStore(IMongoDatabase database)
    {
        _collection = database.GetCollection<PatientConfigurationDocument>("automation_patient_configurations");
    }

    public async Task<List<PatientConfiguration>> GetAllAsync(CancellationToken ct = default)
    {
        var docs = await _collection.Find(FilterDefinition<PatientConfigurationDocument>.Empty)
            .SortBy(d => d.Name)
            .ToListAsync(ct);
        return docs.Select(ToModel).ToList();
    }

    public async Task<PatientConfiguration?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var doc = await _collection.Find(d => d.Id == id).FirstOrDefaultAsync(ct);
        return doc == null ? null : ToModel(doc);
    }

    public async Task UpsertAsync(PatientConfiguration configuration, CancellationToken ct = default)
    {
        var doc = ToDocument(configuration);
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

    private static PatientConfigurationDocument ToDocument(PatientConfiguration model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        Description = model.Description,
        IsSystem = model.IsSystem,
        UpdatedAt = model.UpdatedAt,
        PayloadJson = JsonSerializer.Serialize(model, JsonOpts)
    };

    private static PatientConfiguration ToModel(PatientConfigurationDocument doc)
    {
        var model = Deserialize<PatientConfiguration>(doc.PayloadJson) ?? new PatientConfiguration();
        model.Id = doc.Id;
        model.Name = doc.Name;
        model.Description = doc.Description;
        model.IsSystem = doc.IsSystem;
        model.UpdatedAt = doc.UpdatedAt;
        model.Intent ??= new PatientGenerationIntent();
        return model;
    }

    private static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;
        try { return JsonSerializer.Deserialize<T>(json, JsonOpts); }
        catch { return default; }
    }
}
