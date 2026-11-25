using MongoDB.Driver;

namespace Automation.UI.Services.Persistence;

public sealed class MongoMetricsBenchmarkStore : IMetricsBenchmarkStore
{
    public const string CollectionName = "automation_metrics_benchmarks";

    private readonly IMongoCollection<AutomationMetricsBenchmarkDocument> _collection;

    public MongoMetricsBenchmarkStore(IMongoDatabase database)
    {
        _collection = database.GetCollection<AutomationMetricsBenchmarkDocument>(CollectionName);
    }

    public Task UpsertAsync(AutomationMetricsBenchmarkDocument document, CancellationToken cancellationToken = default)
    {
        return _collection.ReplaceOneAsync(
            d => d.Key == document.Key,
            document,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task<AutomationMetricsBenchmarkDocument?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(d => d.Key == key).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<AutomationMetricsBenchmarkDocument> Records, long TotalCount)> ListPageAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var total = await _collection.CountDocumentsAsync(FilterDefinition<AutomationMetricsBenchmarkDocument>.Empty, cancellationToken: cancellationToken);
        var docs = await _collection.Find(FilterDefinition<AutomationMetricsBenchmarkDocument>.Empty)
            .Sort(Builders<AutomationMetricsBenchmarkDocument>.Sort.Ascending(d => d.Key))
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);
        return (docs, total);
    }
}
