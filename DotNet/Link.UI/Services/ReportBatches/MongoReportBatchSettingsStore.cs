using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Link.UI.Services.ReportBatches;

public sealed class MongoReportBatchSettingsStore(IMongoDatabase database) : IReportBatchSettingsStore
{
    public const string CollectionName = "automation_report_batch_settings";
    public const string DocumentId = "default";

    private readonly IMongoCollection<ReportBatchSettingsDocument> _collection =
        database.GetCollection<ReportBatchSettingsDocument>(CollectionName);

    public async Task<int> GetMaxConcurrencyAsync(CancellationToken cancellationToken = default)
    {
        var document = await _collection
            .Find(row => row.Id == DocumentId)
            .FirstOrDefaultAsync(cancellationToken);
        if (document == null || ReportBatchConcurrencyPolicy.Validate(document.MaxConcurrency) != null)
            return ReportBatchConcurrencyPolicy.DefaultMaxConcurrency;

        return document.MaxConcurrency;
    }

    public Task SaveMaxConcurrencyAsync(int maxConcurrency, CancellationToken cancellationToken = default)
    {
        var error = ReportBatchConcurrencyPolicy.Validate(maxConcurrency);
        if (error != null)
            throw new InvalidOperationException(error);

        var update = Builders<ReportBatchSettingsDocument>.Update
            .SetOnInsert(row => row.Id, DocumentId)
            .Set(row => row.MaxConcurrency, maxConcurrency)
            .Set(row => row.UpdatedAt, DateTimeOffset.UtcNow);

        return _collection.UpdateOneAsync(
            row => row.Id == DocumentId,
            update,
            new UpdateOptions { IsUpsert = true },
            cancellationToken);
    }

    [BsonIgnoreExtraElements]
    private sealed class ReportBatchSettingsDocument
    {
        [BsonId]
        public string Id { get; set; } = DocumentId;

        public int MaxConcurrency { get; set; }

        [BsonRepresentation(BsonType.DateTime)]
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
