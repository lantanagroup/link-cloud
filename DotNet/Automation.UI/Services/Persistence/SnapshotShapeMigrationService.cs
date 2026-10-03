using MongoDB.Bson;
using MongoDB.Driver;

namespace Automation.UI.Services.Persistence;

/// <summary>
/// Translates legacy single-document and blob-backed snapshots into partitioned
/// documents. The pass is idempotent, pages one small batch at a time, and backs
/// off when Cosmos DB throttles. Documents that already fit, and documents that
/// are already partitioned, are skipped. A failure on one document does not stop
/// the rest, and startup is not blocked. After that pass, orphan parts are
/// swept on a timer for the life of the process.
/// </summary>
public sealed class SnapshotShapeMigrationService : BackgroundService
{
    private const int BatchSize = 8;
    private static readonly TimeSpan OrphanGrace = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);
    private readonly IMongoCollection<DomainSnapshotDocument> _snapshots;
    private readonly IMongoCollection<SnapshotPartDocument> _parts;
    private readonly MongoSnapshotStore _store;
    private readonly ILogger<SnapshotShapeMigrationService> _logger;

    public SnapshotShapeMigrationService(
        IMongoDatabase database,
        MongoSnapshotStore store,
        ILogger<SnapshotShapeMigrationService> logger)
    {
        _snapshots = database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        _parts = database.GetCollection<SnapshotPartDocument>(SnapshotPartDocument.CollectionName);
        _store = store;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            var translated = await TranslateSnapshotsAsync(stoppingToken);
            var splitLogs = 0;
            int split;
            do
            {
                split = await _store.SplitOversizedLogChunksAsync(stoppingToken);
                splitLogs += split;
            }
            while (split > 0 && !stoppingToken.IsCancellationRequested);

            var swept = await SweepOrphanPartsAsync(stoppingToken);
            _logger.LogInformation(
                "Snapshot document migration finished. Translated {Translated}. Split log chunks {SplitLogs}. Swept orphan parts {Swept}.",
                translated,
                splitLogs,
                swept);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Snapshot document migration cancelled.");
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Snapshot document migration failed. Legacy snapshots remain readable.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(SweepInterval, stoppingToken);
                var swept = await SweepOrphanPartsAsync(stoppingToken);
                if (swept > 0)
                {
                    _logger.LogInformation("Swept {Swept} orphan snapshot parts.", swept);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Orphan snapshot sweep failed. It will retry.");
            }
        }
    }

    private async Task<int> TranslateSnapshotsAsync(CancellationToken ct)
    {
        var translated = 0;
        ObjectId? after = null;
        while (!ct.IsCancellationRequested)
        {
            var filter = after.HasValue
                ? Builders<DomainSnapshotDocument>.Filter.Gt(d => d.Id, after.Value)
                : FilterDefinition<DomainSnapshotDocument>.Empty;
            var batch = await CosmosThrottle.ExecuteAsync(
                token => _snapshots.Find(filter).SortBy(d => d.Id).Limit(BatchSize).ToListAsync(token),
                ct,
                _logger);
            if (batch.Count == 0)
                break;

            foreach (var document in batch)
            {
                ct.ThrowIfCancellationRequested();
                after = document.Id;
                if (!SnapshotPartitioner.ShouldTranslateStoredData(document.Data))
                    continue;

                try
                {
                    if (await _store.TryUpgradeLegacyDomainAsync(document, ct))
                        translated++;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (CosmosThrottle.IsThrottle(ex))
                {
                    _logger.LogWarning(ex, "Snapshot migration throttled past the retry budget. The document will be tried again on the next start.");
                    return translated;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Snapshot migration skipped one document and will try it again on the next start.");
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
        }

        return translated;
    }

    private async Task<int> SweepOrphanPartsAsync(CancellationToken ct)
    {
        var swept = 0;
        var cutoff = DateTimeOffset.UtcNow - OrphanGrace;
        string? afterId = null;
        var headerCache = new Dictionary<(Guid RunId, string Domain), SnapshotPartitionHeader?>(64);
        var activeGenerations = new HashSet<(Guid RunId, string Domain, string GenerationId)>();
        while (!ct.IsCancellationRequested)
        {
            var filter = afterId == null
                ? Builders<SnapshotPartDocument>.Filter.Lt(p => p.UpdatedAt, cutoff)
                : Builders<SnapshotPartDocument>.Filter.And(
                    Builders<SnapshotPartDocument>.Filter.Lt(p => p.UpdatedAt, cutoff),
                    Builders<SnapshotPartDocument>.Filter.Gt(p => p.Id, afterId));
            var batch = await CosmosThrottle.ExecuteAsync(
                token => _parts.Find(filter).SortBy(p => p.Id).Limit(BatchSize).ToListAsync(token),
                ct,
                _logger);
            if (batch.Count == 0)
                break;

            foreach (var part in batch)
            {
                ct.ThrowIfCancellationRequested();
                afterId = part.Id;
                var key = (part.RunId, part.Domain);
                if (!headerCache.TryGetValue(key, out var header))
                {
                    var snapshot = await _snapshots.Find(s => s.RunId == part.RunId && s.Domain == part.Domain)
                        .FirstOrDefaultAsync(ct);
                    header = snapshot != null && SnapshotPartitionHeader.TryRead(snapshot.Data, out var parsed)
                        ? parsed
                        : null;
                    headerCache[key] = header;
                }

                var orphan = header == null
                    || !string.Equals(header.GenerationId, part.GenerationId, StringComparison.Ordinal)
                    || part.Ordinal >= header.PartCount;
                if (!orphan)
                    continue;

                var generationKey = (part.RunId, part.Domain, part.GenerationId);
                if (activeGenerations.Contains(generationKey))
                    continue;

                var newest = await _parts.Find(p => p.RunId == part.RunId
                        && p.Domain == part.Domain
                        && p.GenerationId == part.GenerationId)
                    .SortByDescending(p => p.UpdatedAt)
                    .Project(p => p.UpdatedAt)
                    .FirstOrDefaultAsync(ct);
                if (newest >= cutoff)
                {
                    activeGenerations.Add(generationKey);
                    continue;
                }

                await CosmosThrottle.ExecuteAsync(
                    token => _parts.DeleteOneAsync(p => p.Id == part.Id, token),
                    ct,
                    _logger);
                swept++;
            }
        }

        return swept;
    }
}
