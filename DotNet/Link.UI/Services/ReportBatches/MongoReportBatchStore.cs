using MongoDB.Driver;

namespace Link.UI.Services.ReportBatches;

public sealed class MongoReportBatchStore(IMongoDatabase database) : IReportBatchStore
{
    public const string CollectionName = "automation_report_batches";

    private readonly IMongoCollection<ReportBatchDocument> _collection =
        database.GetCollection<ReportBatchDocument>(CollectionName);

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        var status = new CreateIndexModel<ReportBatchDocument>(
            Builders<ReportBatchDocument>.IndexKeys.Ascending(batch => batch.Status),
            new CreateIndexOptions { Name = "status" });
        // Cosmos rejects a sort on a field that has no index. History lists the newest batches.
        var created = new CreateIndexModel<ReportBatchDocument>(
            Builders<ReportBatchDocument>.IndexKeys.Descending(batch => batch.CreatedAt),
            new CreateIndexOptions { Name = "createdAt" });
        await _collection.Indexes.CreateOneAsync(status, cancellationToken: cancellationToken);
        await _collection.Indexes.CreateOneAsync(created, cancellationToken: cancellationToken);
    }

    public Task InsertAsync(ReportBatchDocument batch, CancellationToken cancellationToken = default)
        => _collection.InsertOneAsync(batch, cancellationToken: cancellationToken);

    public async Task<ReportBatchDocument?> GetAsync(Guid batchId, CancellationToken cancellationToken = default)
        => await _collection.Find(batch => batch.BatchId == batchId).FirstOrDefaultAsync(cancellationToken);

    public Task ReplaceAsync(ReportBatchDocument batch, CancellationToken cancellationToken = default)
        => _collection.ReplaceOneAsync(
            existing => existing.BatchId == batch.BatchId,
            batch,
            new ReplaceOptions { IsUpsert = false },
            cancellationToken);

    public async Task<IReadOnlyList<ReportBatchDocument>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        var filter = Builders<ReportBatchDocument>.Filter.In(
            batch => batch.Status,
            [ReportBatchStatus.Queued, ReportBatchStatus.Running]);
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReportBatchDocument>> ListRecentAsync(int limit, CancellationToken cancellationToken = default)
    {
        if (limit < 1)
            return [];

        return await _collection
            .Find(FilterDefinition<ReportBatchDocument>.Empty)
            .SortByDescending(batch => batch.CreatedAt)
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }
}
