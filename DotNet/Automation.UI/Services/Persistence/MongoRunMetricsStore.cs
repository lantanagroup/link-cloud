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

    public async Task<AutomationRunMetricsDocument?> GetAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(d => d.RunId == runId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<AutomationRunMetricsDocument> Records, long TotalCount)> ListPageAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var total = await _collection.CountDocumentsAsync(FilterDefinition<AutomationRunMetricsDocument>.Empty, cancellationToken: cancellationToken);
        var docs = await _collection.Find(FilterDefinition<AutomationRunMetricsDocument>.Empty)
            .Sort(Builders<AutomationRunMetricsDocument>.Sort.Descending(d => d.FinishedAt))
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);
        return (docs, total);
    }

    public async Task<IReadOnlyList<AutomationRunMetricsDocument>> ListSinceAsync(
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        var docs = await _collection.Find(d => d.FinishedAt >= since)
            .Sort(Builders<AutomationRunMetricsDocument>.Sort.Descending(d => d.FinishedAt))
            .ToListAsync(cancellationToken);
        return docs;
    }

    public async Task<AutomationRunMetricsDocument?> GetPreviousSucceededAsync(
        Guid scenarioId,
        DateTimeOffset beforeFinishedAt,
        Guid excludeRunId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<AutomationRunMetricsDocument>.Filter.And(
            Builders<AutomationRunMetricsDocument>.Filter.Eq(d => d.ScenarioId, scenarioId),
            Builders<AutomationRunMetricsDocument>.Filter.Eq(d => d.Outcome, "Succeeded"),
            Builders<AutomationRunMetricsDocument>.Filter.Lt(d => d.FinishedAt, beforeFinishedAt),
            Builders<AutomationRunMetricsDocument>.Filter.Ne(d => d.RunId, excludeRunId));

        return await _collection.Find(filter)
            .Sort(Builders<AutomationRunMetricsDocument>.Sort.Descending(d => d.FinishedAt))
            .Limit(1)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
