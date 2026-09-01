using LantanaGroup.Link.Automation.Link.Helpers;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Automation.UI.Services.Persistence;

public interface ICleanupSettingsStore
{
    Task<LeftoverRunCleanupSettings> GetEffectiveAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(LeftoverRunCleanupSettings settings, CancellationToken cancellationToken = default);

    Task RecordDailyTeardownAsync(DateTimeOffset at, string result, CancellationToken cancellationToken = default);

    Task RecordWeeklyPurgeAsync(DateTimeOffset at, string result, CancellationToken cancellationToken = default);
}

public sealed class LeftoverRunCleanupSettings
{
    public bool Enabled { get; set; } = true;
    public bool QuiesceEnabled { get; set; } = true;
    public TimeSpan QuiesceInterval { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan QuiesceGrace { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan TeardownRetention { get; set; } = TimeSpan.FromDays(14);
    public TimeSpan AbortTtl { get; set; } = TimeSpan.FromDays(14);
    public int MaxFacilitiesPerPass { get; set; } = 25;
    public bool DailyTeardownEnabled { get; set; } = true;
    public TimeOnly DailyTeardownTimeUtc { get; set; } = new(10, 0);
    public bool WeeklyHistoryPurgeEnabled { get; set; } = true;
    public DayOfWeek WeeklyHistoryPurgeDay { get; set; } = DayOfWeek.Sunday;
    public TimeOnly WeeklyHistoryPurgeTimeUtc { get; set; } = new(10, 0);
    public TimeSpan CatchUpWindow { get; set; } = TimeSpan.FromHours(3);
    public DateTimeOffset? LastDailyTeardownAt { get; set; }
    public string? LastDailyTeardownResult { get; set; }
    public DateTimeOffset? LastWeeklyPurgeAt { get; set; }
    public string? LastWeeklyPurgeResult { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public bool HasUserOverrides { get; set; }
}

/// <summary>
/// Single-document overlay on <see cref="LeftoverRunCleanupOptions"/> so the Cleanup
/// admin page can change schedule/retention without a redeploy.
/// </summary>
public sealed class MongoCleanupSettingsStore(
    IMongoDatabase database,
    IOptions<LeftoverRunCleanupOptions> options) : ICleanupSettingsStore
{
    public const string CollectionName = "automation_cleanup_settings";
    public const string DefaultId = "default";

    private readonly IMongoCollection<CleanupSettingsDocument> _collection =
        database.GetCollection<CleanupSettingsDocument>(CollectionName);

    public async Task<LeftoverRunCleanupSettings> GetEffectiveAsync(CancellationToken cancellationToken = default)
    {
        var defaults = FromOptions(options.Value);
        var doc = await _collection.Find(d => d.Id == DefaultId).FirstOrDefaultAsync(cancellationToken);
        if (doc == null)
            return defaults;

        defaults.LastDailyTeardownAt = doc.LastDailyTeardownAt;
        defaults.LastDailyTeardownResult = doc.LastDailyTeardownResult;
        defaults.LastWeeklyPurgeAt = doc.LastWeeklyPurgeAt;
        defaults.LastWeeklyPurgeResult = doc.LastWeeklyPurgeResult;
        defaults.UpdatedAt = doc.UpdatedAt;
        defaults.HasUserOverrides = doc.HasUserOverrides;

        if (!doc.HasUserOverrides)
            return defaults;

        defaults.Enabled = doc.Enabled;
        defaults.QuiesceEnabled = doc.QuiesceEnabled;
        defaults.QuiesceInterval = TimeSpan.FromMinutes(Math.Max(1, doc.QuiesceIntervalMinutes));
        defaults.QuiesceGrace = TimeSpan.FromMinutes(Math.Max(0, doc.QuiesceGraceMinutes));
        defaults.TeardownRetention = TimeSpan.FromDays(Math.Max(1, doc.TeardownRetentionDays));
        defaults.AbortTtl = TimeSpan.FromDays(Math.Max(1, doc.AbortTtlDays));
        defaults.MaxFacilitiesPerPass = Math.Max(1, doc.MaxFacilitiesPerPass);
        defaults.DailyTeardownEnabled = doc.DailyTeardownEnabled;
        defaults.DailyTeardownTimeUtc = CleanupSchedule.ParseTimeUtc(doc.DailyTeardownTimeUtc, defaults.DailyTeardownTimeUtc);
        defaults.WeeklyHistoryPurgeEnabled = doc.WeeklyHistoryPurgeEnabled;
        if (Enum.TryParse<DayOfWeek>(doc.WeeklyHistoryPurgeDay, ignoreCase: true, out var day))
            defaults.WeeklyHistoryPurgeDay = day;
        defaults.WeeklyHistoryPurgeTimeUtc = CleanupSchedule.ParseTimeUtc(doc.WeeklyHistoryPurgeTimeUtc, defaults.WeeklyHistoryPurgeTimeUtc);
        defaults.CatchUpWindow = TimeSpan.FromHours(Math.Max(1, doc.CatchUpWindowHours));
        return defaults;
    }

    public async Task SaveAsync(LeftoverRunCleanupSettings settings, CancellationToken cancellationToken = default)
    {
        var existing = await _collection.Find(d => d.Id == DefaultId).FirstOrDefaultAsync(cancellationToken);
        var doc = ToDocument(settings);
        doc.LastDailyTeardownAt = existing?.LastDailyTeardownAt;
        doc.LastDailyTeardownResult = existing?.LastDailyTeardownResult;
        doc.LastWeeklyPurgeAt = existing?.LastWeeklyPurgeAt;
        doc.LastWeeklyPurgeResult = existing?.LastWeeklyPurgeResult;
        doc.HasUserOverrides = true;
        doc.UpdatedAt = DateTimeOffset.UtcNow;

        await _collection.ReplaceOneAsync(
            d => d.Id == DefaultId,
            doc,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public Task RecordDailyTeardownAsync(DateTimeOffset at, string result, CancellationToken cancellationToken = default)
        => RecordAsync(
            Builders<CleanupSettingsDocument>.Update
                .SetOnInsert(d => d.Id, DefaultId)
                .Set(d => d.LastDailyTeardownAt, at)
                .Set(d => d.LastDailyTeardownResult, result)
                .Set(d => d.UpdatedAt, DateTimeOffset.UtcNow),
            cancellationToken);

    public Task RecordWeeklyPurgeAsync(DateTimeOffset at, string result, CancellationToken cancellationToken = default)
        => RecordAsync(
            Builders<CleanupSettingsDocument>.Update
                .SetOnInsert(d => d.Id, DefaultId)
                .Set(d => d.LastWeeklyPurgeAt, at)
                .Set(d => d.LastWeeklyPurgeResult, result)
                .Set(d => d.UpdatedAt, DateTimeOffset.UtcNow),
            cancellationToken);

    private Task RecordAsync(UpdateDefinition<CleanupSettingsDocument> update, CancellationToken cancellationToken)
        => _collection.UpdateOneAsync(
            d => d.Id == DefaultId,
            update,
            new UpdateOptions { IsUpsert = true },
            cancellationToken);

    private static LeftoverRunCleanupSettings FromOptions(LeftoverRunCleanupOptions options) => new()
    {
        Enabled = options.Enabled,
        QuiesceEnabled = options.QuiesceEnabled,
        QuiesceInterval = options.Interval,
        QuiesceGrace = options.QuiesceGrace,
        TeardownRetention = options.TeardownRetention,
        AbortTtl = options.AbortTtl,
        MaxFacilitiesPerPass = Math.Max(1, options.MaxFacilitiesPerPass),
        DailyTeardownEnabled = options.DailyTeardownEnabled,
        DailyTeardownTimeUtc = CleanupSchedule.ParseTimeUtc(options.DailyTeardownTimeUtc, new TimeOnly(10, 0)),
        WeeklyHistoryPurgeEnabled = options.WeeklyHistoryPurgeEnabled,
        WeeklyHistoryPurgeDay = options.WeeklyHistoryPurgeDay,
        WeeklyHistoryPurgeTimeUtc = CleanupSchedule.ParseTimeUtc(options.WeeklyHistoryPurgeTimeUtc, new TimeOnly(10, 0)),
        CatchUpWindow = options.CatchUpWindow <= TimeSpan.Zero ? TimeSpan.FromHours(3) : options.CatchUpWindow
    };

    private static CleanupSettingsDocument ToDocument(LeftoverRunCleanupSettings settings) => new()
    {
        Id = DefaultId,
        Enabled = settings.Enabled,
        QuiesceEnabled = settings.QuiesceEnabled,
        QuiesceIntervalMinutes = Math.Max(1, (int)Math.Round(settings.QuiesceInterval.TotalMinutes)),
        QuiesceGraceMinutes = Math.Max(0, (int)Math.Round(settings.QuiesceGrace.TotalMinutes)),
        TeardownRetentionDays = Math.Max(1, (int)Math.Round(settings.TeardownRetention.TotalDays)),
        AbortTtlDays = Math.Max(1, (int)Math.Round(settings.AbortTtl.TotalDays)),
        MaxFacilitiesPerPass = Math.Max(1, settings.MaxFacilitiesPerPass),
        DailyTeardownEnabled = settings.DailyTeardownEnabled,
        DailyTeardownTimeUtc = settings.DailyTeardownTimeUtc.ToString("HH:mm"),
        WeeklyHistoryPurgeEnabled = settings.WeeklyHistoryPurgeEnabled,
        WeeklyHistoryPurgeDay = settings.WeeklyHistoryPurgeDay.ToString(),
        WeeklyHistoryPurgeTimeUtc = settings.WeeklyHistoryPurgeTimeUtc.ToString("HH:mm"),
        CatchUpWindowHours = Math.Max(1, (int)Math.Round(settings.CatchUpWindow.TotalHours)),
        HasUserOverrides = true,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    [BsonIgnoreExtraElements]
    private sealed class CleanupSettingsDocument
    {
        [BsonId]
        public string Id { get; set; } = DefaultId;

        public bool Enabled { get; set; } = true;
        public bool QuiesceEnabled { get; set; } = true;
        public int QuiesceIntervalMinutes { get; set; } = 5;
        public int QuiesceGraceMinutes { get; set; } = 2;
        public int TeardownRetentionDays { get; set; } = 14;
        public int AbortTtlDays { get; set; } = 14;
        public int MaxFacilitiesPerPass { get; set; } = 25;
        public bool DailyTeardownEnabled { get; set; } = true;
        public string DailyTeardownTimeUtc { get; set; } = "10:00";
        public bool WeeklyHistoryPurgeEnabled { get; set; } = true;
        public string WeeklyHistoryPurgeDay { get; set; } = "Sunday";
        public string WeeklyHistoryPurgeTimeUtc { get; set; } = "10:00";
        public int CatchUpWindowHours { get; set; } = 3;
        public bool HasUserOverrides { get; set; }

        [BsonRepresentation(MongoDB.Bson.BsonType.DateTime)]
        public DateTimeOffset? LastDailyTeardownAt { get; set; }

        public string? LastDailyTeardownResult { get; set; }

        [BsonRepresentation(MongoDB.Bson.BsonType.DateTime)]
        public DateTimeOffset? LastWeeklyPurgeAt { get; set; }

        public string? LastWeeklyPurgeResult { get; set; }

        [BsonRepresentation(MongoDB.Bson.BsonType.DateTime)]
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
