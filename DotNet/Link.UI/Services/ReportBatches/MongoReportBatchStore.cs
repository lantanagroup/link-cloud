using MongoDB.Driver;

namespace Link.UI.Services.ReportBatches;

public sealed class MongoReportBatchStore(IMongoDatabase database) : IReportBatchStore
{
    public const string CollectionName = "automation_report_batches";

    private readonly IMongoCollection<ReportBatchDocument> _collection =
        database.GetCollection<ReportBatchDocument>(CollectionName);

    public Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        var keys = Builders<ReportBatchDocument>.IndexKeys.Ascending(batch => batch.Status);
        var model = new CreateIndexModel<ReportBatchDocument>(
            keys,
            new CreateIndexOptions { Name = "status" });
        return _collection.Indexes.CreateOneAsync(model, cancellationToken: cancellationToken);
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
}
