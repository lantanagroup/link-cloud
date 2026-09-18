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
public sealed class MongoCleanupReportStore(IMongoDatabase database) : ICleanupReportStore
{
    public const string CollectionName = "automation_cleanup_reports";
    public const int MaxStoredReports = 50;

    private readonly IMongoCollection<CleanupReportDocument> _collection =
        database.GetCollection<CleanupReportDocument>(CollectionName);

    public async Task SaveAsync(CleanupReport report, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(ToDocument(report), cancellationToken: cancellationToken);
        await TrimAsync(cancellationToken);
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
        var extra = await _collection
            .Find(_ => true)
            .SortByDescending(d => d.FinishedAt)
            .Skip(MaxStoredReports)
            .Project(d => d.Id)
            .ToListAsync(cancellationToken);
        if (extra.Count == 0)
            return;

        await _collection.DeleteManyAsync(d => extra.Contains(d.Id), cancellationToken);
    }

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
        public Guid Id { get; set; }
        public string Mode { get; set; } = "";
        public string Label { get; set; } = "";
        public string Trigger { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset FinishedAt { get; set; }
        public int QuiesceCandidateCount { get; set; }
        public List<string> QuiescedFacilityIds { get; set; } = [];
        public int TeardownCandidateCount { get; set; }
        public List<string> TornDownFacilityIds { get; set; } = [];
        public int HistoryPurgeCandidateCount { get; set; }
        public List<Guid> PurgedRunIds { get; set; } = [];
        public List<string> FailedFacilityIds { get; set; } = [];
        public List<Guid> FailedRunIds { get; set; } = [];
        public string Message { get; set; } = "";
    }
}
