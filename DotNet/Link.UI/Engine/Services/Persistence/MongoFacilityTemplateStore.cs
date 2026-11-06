using Automation.UI.Models;
using MongoDB.Driver;

namespace Automation.UI.Services.Persistence;

public sealed class MongoFacilityTemplateStore : IFacilityTemplateStore
{
    public const string CollectionName = "automation_facility_templates";

    private readonly IMongoCollection<FacilityTemplateDocument> _collection;

    public MongoFacilityTemplateStore(IMongoDatabase database)
    {
        _collection = database.GetCollection<FacilityTemplateDocument>(CollectionName);
    }

    public async Task<List<FacilityTemplate>> GetAllAsync(CancellationToken ct = default)
    {
        var docs = await _collection.Find(FilterDefinition<FacilityTemplateDocument>.Empty)
            .SortBy(d => d.Name)
            .ToListAsync(ct);
        return docs.Select(ToModel).ToList();
    }

    public async Task<FacilityTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var doc = await _collection.Find(d => d.Id == id).FirstOrDefaultAsync(ct);
        return doc == null ? null : ToModel(doc);
    }

    public async Task<FacilityTemplate?> GetDefaultAsync(CancellationToken ct = default)
    {
        var doc = await _collection.Find(d => d.IsDefault).FirstOrDefaultAsync(ct);
        return doc == null ? null : ToModel(doc);
    }

    public async Task UpsertAsync(FacilityTemplate template, CancellationToken ct = default)
    {
        await _collection.ReplaceOneAsync(
            d => d.Id == template.Id,
            ToDocument(template),
            new ReplaceOptions { IsUpsert = true },
            ct);
    }

    public async Task SetDefaultAsync(Guid id, CancellationToken ct = default)
    {
        var clear = Builders<FacilityTemplateDocument>.Update.Set(d => d.IsDefault, false);
        await _collection.UpdateManyAsync(_ => true, clear, cancellationToken: ct);
        var set = Builders<FacilityTemplateDocument>.Update.Set(d => d.IsDefault, true);
        await _collection.UpdateOneAsync(d => d.Id == id, set, cancellationToken: ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _collection.DeleteOneAsync(d => d.Id == id, ct);
    }

    private static FacilityTemplateDocument ToDocument(FacilityTemplate model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        Description = model.Description,
        IsSystem = model.IsSystem,
        IsDefault = model.IsDefault,
        VendorName = model.VendorName,
        QueryPlanTemplateId = model.QueryPlanTemplateId,
        NormalizationSuiteId = model.NormalizationSuiteId,
        OrganizationResourceMapTemplateId = model.OrganizationResourceMapTemplateId,
        EnableOrganizationLocationMapping = model.EnableOrganizationLocationMapping,
        AllowedPatientConfigurationIds = model.AllowedPatientConfigurationIds.Select(id => id.ToString()).ToList(),
        AllowPatientConfigurationsOutsideSet = model.AllowPatientConfigurationsOutsideSet,
        UpdatedAt = model.UpdatedAt
    };

    private static FacilityTemplate ToModel(FacilityTemplateDocument doc) => new()
    {
        Id = doc.Id,
        Name = doc.Name,
        Description = doc.Description,
        IsSystem = doc.IsSystem,
        IsDefault = doc.IsDefault,
        VendorName = doc.VendorName,
        QueryPlanTemplateId = doc.QueryPlanTemplateId,
        NormalizationSuiteId = doc.NormalizationSuiteId,
        OrganizationResourceMapTemplateId = doc.OrganizationResourceMapTemplateId,
        EnableOrganizationLocationMapping = doc.EnableOrganizationLocationMapping,
        AllowedPatientConfigurationIds = (doc.AllowedPatientConfigurationIds ?? [])
            .Select(s => Guid.TryParse(s, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToList(),
        AllowPatientConfigurationsOutsideSet = doc.AllowPatientConfigurationsOutsideSet,
        UpdatedAt = doc.UpdatedAt
    };
}
