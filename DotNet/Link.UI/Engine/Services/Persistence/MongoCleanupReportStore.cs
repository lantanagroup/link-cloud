using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Automation.UI.Services.Persistence;

public interface ICleanupReportStore
{
    Task SaveAsync(CleanupReport report, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CleanupReport>> ListRecentAsync(int limit = 25, CancellationToken cancellationToken = default);

    Task<CleanupReport?> GetAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class CleanupReport
{
    public Guid Id { get; set; }
    public string Mode { get; set; } = "";
    public string Label { get; set; } = "";
    public string Trigger { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset FinishedAt { get; set; }
    public int QuiesceCandidateCount { get; set; }
    public IReadOnlyList<string> QuiescedFacilityIds { get; set; } = [];
    public int TeardownCandidateCount { get; set; }
    public IReadOnlyList<string> TornDownFacilityIds { get; set; } = [];
    public int HistoryPurgeCandidateCount { get; set; }
    public IReadOnlyList<Guid> PurgedRunIds { get; set; } = [];
    public IReadOnlyList<string> FailedFacilityIds { get; set; } = [];
    public IReadOnlyList<Guid> FailedRunIds { get; set; } = [];
    public string Message { get; set; } = "";
}

/// <summary>
/// One document per leftover-cleanup pass so operators can reopen completed work.
/// Kept small: each pass is capped by MaxFacilitiesPerPass.
/// </summary>
public sealed class MongoCleanupReportStore(IMongoDatabase database, ILogger<MongoCleanupReportStore> logger) : ICleanupReportStore
{
    public const string CollectionName = "automation_cleanup_reports";
    public const int MaxStoredReports = 50;

    private readonly IMongoCollection<CleanupReportDocument> _collection =
        database.GetCollection<CleanupReportDocument>(CollectionName);

    public async Task SaveAsync(CleanupReport report, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(ToDocument(report), cancellationToken: cancellationToken);
        try
        {
            await TrimAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Cleanup report trim was cancelled after saving {ReportId}.", report.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not trim leftover cleanup reports after saving {ReportId}.", report.Id);
        }
    }

    public async Task<IReadOnlyList<CleanupReport>> ListRecentAsync(int limit = 25, CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(limit, 1, MaxStoredReports);
        var docs = await _collection
            .Find(_ => true)
            .SortByDescending(d => d.FinishedAt)
            .Limit(take)
            .ToListAsync(cancellationToken);
        return docs.Select(FromDocument).ToList();
    }

    public async Task<CleanupReport?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var doc = await _collection.Find(d => d.Id == id).FirstOrDefaultAsync(cancellationToken);
        return doc == null ? null : FromDocument(doc);
    }

    private async Task TrimAsync(CancellationToken cancellationToken)
    {
        var docs = await _collection.Find(_ => true).ToListAsync(cancellationToken);
        var drop = SelectIdsToTrim(docs.Select(FromDocument).ToList(), MaxStoredReports);
        if (drop.Count == 0)
            return;

        await _collection.DeleteManyAsync(d => drop.Contains(d.Id), cancellationToken);
    }

    /// <summary>
    /// Drops nothing-matched passes before any pass that quiesced, tore down, purged, or failed.
    /// Within each group, older finishes go first.
    /// </summary>
    public static IReadOnlyList<Guid> SelectIdsToTrim(IReadOnlyList<CleanupReport> reports, int maxStored)
    {
        if (reports.Count <= maxStored)
            return [];

        return reports
            .OrderBy(report => DidWork(report) ? 1 : 0)
            .ThenBy(report => report.FinishedAt)
            .Take(reports.Count - maxStored)
            .Select(report => report.Id)
            .ToList();
    }

    private static bool DidWork(CleanupReport report)
        => report.Status == "failed"
           || report.QuiescedFacilityIds.Count > 0
           || report.TornDownFacilityIds.Count > 0
           || report.PurgedRunIds.Count > 0
           || report.FailedFacilityIds.Count > 0
           || report.FailedRunIds.Count > 0;

    private static CleanupReportDocument ToDocument(CleanupReport report) => new()
    {
        Id = report.Id == Guid.Empty ? Guid.NewGuid() : report.Id,
        Mode = report.Mode,
        Label = report.Label,
        Trigger = report.Trigger,
        Status = report.Status,
        StartedAt = report.StartedAt,
        FinishedAt = report.FinishedAt,
        QuiesceCandidateCount = report.QuiesceCandidateCount,
        QuiescedFacilityIds = report.QuiescedFacilityIds.ToList(),
        TeardownCandidateCount = report.TeardownCandidateCount,
        TornDownFacilityIds = report.TornDownFacilityIds.ToList(),
        HistoryPurgeCandidateCount = report.HistoryPurgeCandidateCount,
        PurgedRunIds = report.PurgedRunIds.ToList(),
        FailedFacilityIds = report.FailedFacilityIds.ToList(),
        FailedRunIds = report.FailedRunIds.ToList(),
        Message = report.Message
    };

    private static CleanupReport FromDocument(CleanupReportDocument doc) => new()
    {
        Id = doc.Id,
        Mode = doc.Mode,
        Label = doc.Label,
        Trigger = doc.Trigger,
        Status = doc.Status,
        StartedAt = doc.StartedAt,
        FinishedAt = doc.FinishedAt,
        QuiesceCandidateCount = doc.QuiesceCandidateCount,
        QuiescedFacilityIds = doc.QuiescedFacilityIds,
        TeardownCandidateCount = doc.TeardownCandidateCount,
        TornDownFacilityIds = doc.TornDownFacilityIds,
        HistoryPurgeCandidateCount = doc.HistoryPurgeCandidateCount,
        PurgedRunIds = doc.PurgedRunIds,
        FailedFacilityIds = doc.FailedFacilityIds,
        FailedRunIds = doc.FailedRunIds,
        Message = doc.Message
    };

    [BsonIgnoreExtraElements]
    private sealed class CleanupReportDocument
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public Guid Id { get; set; }
        public string Mode { get; set; } = "";
        public string Label { get; set; } = "";
        public string Trigger { get; set; } = "";
        public string Status { get; set; } = "";
        [BsonRepresentation(BsonType.DateTime)]
        public DateTimeOffset StartedAt { get; set; }
        [BsonRepresentation(BsonType.DateTime)]
        public DateTimeOffset FinishedAt { get; set; }
        public int QuiesceCandidateCount { get; set; }
        public List<string> QuiescedFacilityIds { get; set; } = [];
        public int TeardownCandidateCount { get; set; }
        public List<string> TornDownFacilityIds { get; set; } = [];
        public int HistoryPurgeCandidateCount { get; set; }
        [BsonSerializer(typeof(GuidStringListSerializer))]
        public List<Guid> PurgedRunIds { get; set; } = [];
        public List<string> FailedFacilityIds { get; set; } = [];
        [BsonSerializer(typeof(GuidStringListSerializer))]
        public List<Guid> FailedRunIds { get; set; } = [];
        public string Message { get; set; } = "";
    }
}
