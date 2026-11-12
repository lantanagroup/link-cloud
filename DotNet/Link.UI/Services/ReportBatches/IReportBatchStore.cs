namespace Link.UI.Services.ReportBatches;

public interface IReportBatchStore
{
    Task EnsureIndexesAsync(CancellationToken cancellationToken = default);

    Task InsertAsync(ReportBatchDocument batch, CancellationToken cancellationToken = default);

    Task<ReportBatchDocument?> GetAsync(Guid batchId, CancellationToken cancellationToken = default);

    Task ReplaceAsync(ReportBatchDocument batch, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReportBatchDocument>> ListActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Newest batches first. The caller chooses how many.</summary>
    Task<IReadOnlyList<ReportBatchDocument>> ListRecentAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Every batch that belongs to one launch. The caller sorts them.</summary>
    Task<IReadOnlyList<ReportBatchDocument>> ListByLaunchAsync(Guid launchId, CancellationToken cancellationToken = default);
}

public interface IReportBatchSettingsStore
{
    Task<int> GetMaxConcurrencyAsync(CancellationToken cancellationToken = default);

    Task SaveMaxConcurrencyAsync(int maxConcurrency, CancellationToken cancellationToken = default);
}
