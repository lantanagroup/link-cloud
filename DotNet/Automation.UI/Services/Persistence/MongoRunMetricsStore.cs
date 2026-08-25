using MongoDB.Driver;

namespace Automation.UI.Services.Persistence;

public sealed class MongoRunMetricsStore : IRunMetricsStore
{
    public const string CollectionName = "automation_run_metrics";

    private readonly IMongoCollection<AutomationRunMetricsDocument> _collection;

    public MongoRunMetricsStore(IMongoDatabase database)
    {
        _collection = database.GetCollection<AutomationRunMetricsDocument>(CollectionName);
    }

    public Task UpsertAsync(AutomationRunMetricsDocument document, CancellationToken cancellationToken = default)
    {
        return _collection.ReplaceOneAsync(
            d => d.RunId == document.RunId,
            document,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public Task<AutomationRunMetricsDocument?> GetAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        return _collection.Find(d => d.RunId == runId).FirstOrDefaultAsync(cancellationToken);
    }
}
