using Automation.UI.Models;
using MongoDB.Driver;
using System.Text.RegularExpressions;

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

    public async Task<long> CountFilteredAsync(
    string? search,
    string? vendor,
    string? kind,
    CancellationToken ct = default)
    {
        var filter = BuildFilter(search, vendor, kind);

        return await _collection.CountDocumentsAsync(
            filter,
            cancellationToken: ct);
    }
    public async Task<List<FacilityTemplate>> GetFilteredPageAsync(
        int skip,
        int take,
        string? search,
        string? vendor,
        string? kind,
        string sortBy = "name",
        string sortDir = "asc",
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);

        var filter = BuildFilter(search, vendor, kind);

        // Retrieve only fields required for sorting and pagination.
        // Avoid database-side sorting on VendorName or IsSystem,
        // which may not be indexed in Cosmos DB for MongoDB RU.
        var lightweightDocs = await _collection
            .Find(filter)
            .Project(d => new
            {
                d.Id,
                d.Name,
                d.VendorName,
                d.IsSystem
            })
            .ToListAsync(ct);

        var descending = string.Equals(
            sortDir, "desc", StringComparison.OrdinalIgnoreCase);

        var normalizedSortBy = sortBy.ToLowerInvariant();

        var ordered = normalizedSortBy switch
        {
            "vendor" => descending
                ? lightweightDocs.OrderByDescending(
                    d => d.VendorName, StringComparer.Ordinal)
                : lightweightDocs.OrderBy(
                    d => d.VendorName, StringComparer.Ordinal),

            "kind" => descending
                ? lightweightDocs.OrderByDescending(d => d.IsSystem)
                : lightweightDocs.OrderBy(d => d.IsSystem),

            _ => descending
                ? lightweightDocs.OrderByDescending(
                    d => d.Name, StringComparer.Ordinal)
                : lightweightDocs.OrderBy(
                    d => d.Name, StringComparer.Ordinal)
        };

        var pageIds = ordered
            .ThenBy(d => d.Name, StringComparer.Ordinal)
            .ThenBy(d => d.Id)
            .Skip(skip)
            .Take(take)
            .Select(d => d.Id)
            .ToList();

        if (pageIds.Count == 0)
            return [];

        // Load full documents only for the selected page.
        var pageFilter = Builders<FacilityTemplateDocument>
            .Filter.In(d => d.Id, pageIds);

        var pageDocs = await _collection
            .Find(pageFilter)
            .ToListAsync(ct);

        // MongoDB's $in query does not guarantee result ordering.
        // Restore the exact order selected during pagination.
        var docsById = pageDocs.ToDictionary(d => d.Id);

        return pageIds
            .Where(id => docsById.ContainsKey(id))
            .Select(id => ToModel(docsById[id]))
            .ToList();
    }

    private static FilterDefinition<FacilityTemplateDocument> BuildFilter(
        string? search,
        string? vendor,
        string? kind)
    {
        var builder = Builders<FacilityTemplateDocument>.Filter;
        var filters = new List<FilterDefinition<FacilityTemplateDocument>>();

        if (!string.IsNullOrWhiteSpace(search))
        {
            filters.Add(builder.Regex(
                d => d.Name,
                new MongoDB.Bson.BsonRegularExpression(
                    Regex.Escape(search.Trim()), "i")));
        }

        if (!string.IsNullOrWhiteSpace(vendor))
        {
            filters.Add(builder.Regex(
                d => d.VendorName,
                new MongoDB.Bson.BsonRegularExpression(
                    $"^{Regex.Escape(vendor.Trim())}$", "i")));
        }

        if (string.Equals(kind, "System", StringComparison.OrdinalIgnoreCase))
        {
            filters.Add(builder.Eq(d => d.IsSystem, true));
        }
        else if (string.Equals(kind, "Custom", StringComparison.OrdinalIgnoreCase))
        {
            filters.Add(builder.Eq(d => d.IsSystem, false));
        }

        return filters.Count == 0
            ? builder.Empty
            : builder.And(filters);
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
