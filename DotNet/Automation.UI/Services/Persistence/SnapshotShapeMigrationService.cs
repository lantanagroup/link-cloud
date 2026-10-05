using MongoDB.Bson;
using MongoDB.Driver;

namespace Automation.UI.Services.Persistence;

/// <summary>
/// Translates legacy single-document and blob-backed snapshots into partitioned
/// documents, and rewrites stored run-history payloads down to chart summaries.
/// The pass is idempotent, pages one small batch at a time, and backs
/// off when Cosmos DB throttles. Documents that already fit and already use the
/// summary shape are skipped, and so are documents that are already partitioned.
/// Normalization evidence chunk documents are deleted. A failure on one document
/// does not stop the rest, and startup is not blocked. After that pass, orphan
/// parts are swept on a timer for the life of the process.
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

    /// <summary>
    /// Test hook invoked once, after a generation looks orphaned and before it
    /// is deleted. Production leaves it null. The sweep re-reads the header
    /// after the hook returns.
    /// </summary>
    internal Func<CancellationToken, Task>? BeforeOrphanPartDelete { get; set; }

    /// <summary>How many partition headers this instance has loaded. Tests use it.</summary>
    internal int PartitionHeaderReads { get; private set; }

    /// <summary>
    /// Test hook invoked before a legacy snapshot is translated. Production
    /// leaves it null. A throttle from this hook must not skip later documents.
    /// </summary>
    internal Func<DomainSnapshotDocument, CancellationToken, Task>? BeforeTranslateDocumentForTests { get; set; }

    internal void RememberLiveConfirmationForTests(
        Guid runId,
        string domain,
        string generationId,
        DateTimeOffset confirmedAt)
        => _liveSettledConfirmedAt[(runId, domain, generationId)] = confirmedAt;

    internal bool HasLiveConfirmationForTests(Guid runId, string domain, string generationId)
        => _liveSettledConfirmedAt.ContainsKey((runId, domain, generationId));

    private bool _settledBackfillComplete;
    private string? _settledSweepAfterId;
    private readonly Dictionary<(Guid RunId, string Domain, string GenerationId), DateTimeOffset> _liveSettledConfirmedAt = new();

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
                var split = await _store.SplitOversizedLogChunksAsync(stoppingToken);
                if (swept > 0 || split > 0)
                {
                    _logger.LogInformation("Swept {Swept} orphan snapshot parts. Split log chunks {Split}.", swept, split);
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

    internal async Task<int> TranslateSnapshotsAsync(CancellationToken ct)
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
                if (RunHistorySlim.IsNormalizationEvidenceChunkDomain(document.Domain))
                {
                    try
                    {
                        if (BeforeTranslateDocumentForTests != null)
                            await BeforeTranslateDocumentForTests(document, ct);

                        await _store.DeleteDomainSnapshotAsync(document.RunId, document.Domain, ct);
                        translated++;
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex) when (CosmosThrottle.IsThrottle(ex))
                    {
                        _logger.LogWarning(
                            ex,
                            "Snapshot migration left one document for the next start and continued with the rest.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Snapshot migration skipped one document and will try it again on the next start.");
                    }

                    continue;
                }

                if (!SnapshotPartitioner.ShouldTranslateStoredData(document.Data)
                    && RunHistorySlim.TrySlimStoredJson(document.Domain, document.Data) == null)
                {
                    continue;
                }

                try
                {
                    if (BeforeTranslateDocumentForTests != null)
                        await BeforeTranslateDocumentForTests(document, ct);

                    if (await _store.TryUpgradeLegacyDomainAsync(document, ct))
                        translated++;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (CosmosThrottle.IsThrottle(ex))
                {
                    _logger.LogWarning(
                        ex,
                        "Snapshot migration left one document for the next start and continued with the rest.");
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

    internal async Task<int> SweepOrphanPartsAsync(CancellationToken ct)
    {
        await _store.SweepRetiredGenerationsAsync(ct);
        await BackfillMissingSettledAsync(ct);
        await SweepSettledGenerationsAsync(ct);
        var swept = 0;
        var cutoff = DateTimeOffset.UtcNow - OrphanGrace;
        string? afterId = null;
        var activeGenerations = new HashSet<(Guid RunId, string Domain, string GenerationId)>();
        var markedSettled = new HashSet<(Guid RunId, string Domain, string GenerationId)>();
        var headerCache = new Dictionary<(Guid RunId, string Domain), SnapshotPartitionHeader?>();
        while (!ct.IsCancellationRequested)
        {
            var aged = Builders<SnapshotPartDocument>.Filter.And(
                Builders<SnapshotPartDocument>.Filter.Eq(p => p.Settled, false),
                Builders<SnapshotPartDocument>.Filter.Lt(p => p.UpdatedAt, cutoff));
            var filter = afterId == null
                ? aged
                : Builders<SnapshotPartDocument>.Filter.And(
                    aged,
                    Builders<SnapshotPartDocument>.Filter.Gt(p => p.Id, afterId));
            var batch = await CosmosThrottle.ExecuteAsync(
                token => _parts.Find(filter)
                    .SortBy(p => p.Id)
                    .Limit(BatchSize)
                    .Project(p => new PartMeta
                    {
                        Id = p.Id,
                        RunId = p.RunId,
                        Domain = p.Domain,
                        GenerationId = p.GenerationId,
                        Ordinal = p.Ordinal,
                        UpdatedAt = p.UpdatedAt
                    })
                    .ToListAsync(token),
                ct,
                _logger);
            if (batch.Count == 0)
                break;

            foreach (var part in batch)
            {
                ct.ThrowIfCancellationRequested();
                afterId = part.Id;
                var cacheKey = (part.RunId, part.Domain);
                if (!headerCache.TryGetValue(cacheKey, out var header))
                {
                    header = await ReadPartitionHeaderAsync(part.RunId, part.Domain, ct);
                    headerCache[cacheKey] = header;
                }

                if (!IsOrphanPart(header, part))
                {
                    RememberLiveGeneration(header, part);
                    await MarkSettledOnceAsync(markedSettled, part, ct);
                    continue;
                }

                var generationKey = (part.RunId, part.Domain, part.GenerationId);
                if (activeGenerations.Contains(generationKey))
                    continue;

                // Run, domain, and generation are indexed. UpdatedAt is not a
                // sort key on that index, and Cosmos rejects the sort.
                var freshId = await _parts.Find(p => p.RunId == part.RunId
                        && p.Domain == part.Domain
                        && p.GenerationId == part.GenerationId
                        && p.UpdatedAt >= cutoff)
                    .Limit(1)
                    .Project(p => p.Id)
                    .FirstOrDefaultAsync(ct);
                if (!string.IsNullOrEmpty(freshId))
                {
                    activeGenerations.Add(generationKey);
                    continue;
                }

                if (BeforeOrphanPartDelete != null)
                {
                    var callback = BeforeOrphanPartDelete;
                    BeforeOrphanPartDelete = null;
                    await callback(ct);
                }

                // The cached header can be older than a commit that landed while
                // this generation was being judged. Decide from a fresh read.
                header = await ReadPartitionHeaderAsync(part.RunId, part.Domain, ct);
                headerCache[cacheKey] = header;
                if (!IsOrphanPart(header, part))
                {
                    activeGenerations.Add(generationKey);
                    RememberLiveGeneration(header, part);
                    await MarkSettledOnceAsync(markedSettled, part, ct);
                    continue;
                }

                var deleteFilter = Builders<SnapshotPartDocument>.Filter.And(
                    Builders<SnapshotPartDocument>.Filter.Eq(p => p.RunId, part.RunId),
                    Builders<SnapshotPartDocument>.Filter.Eq(p => p.Domain, part.Domain),
                    Builders<SnapshotPartDocument>.Filter.Eq(p => p.GenerationId, part.GenerationId),
                    Builders<SnapshotPartDocument>.Filter.Lt(p => p.UpdatedAt, cutoff));
                if (header != null
                    && string.Equals(header.GenerationId, part.GenerationId, StringComparison.Ordinal))
                {
                    deleteFilter &= Builders<SnapshotPartDocument>.Filter.Gte(p => p.Ordinal, header.PartCount);
                }

                var deleted = await CosmosThrottle.ExecuteAsync(
                    token => _parts.DeleteManyAsync(deleteFilter, token),
                    ct,
                    _logger);
                swept += (int)deleted.DeletedCount;
                activeGenerations.Add(generationKey);
            }
        }

        return swept;
    }

    /// <summary>
    /// Deletes settled generations that are older than the grace period and are
    /// not the current header. A crash after publish, or a later commit that
    /// retired only the generation it replaced, leaves these parts behind.
    /// The pass is bounded to one batch and does not load part bodies.
    /// Confirming the live generation moves the cursor past that generation's
    /// part ids, and never backward, so a long retained snapshot does not
    /// stall a later orphan. An empty page still wraps to the start.
    /// </summary>
    private async Task SweepSettledGenerationsAsync(CancellationToken ct)
    {
        PruneExpiredLiveConfirmations(DateTimeOffset.UtcNow);
        var cutoff = DateTimeOffset.UtcNow - OrphanGrace;
        var batch = await PageSettledPartsAsync(_settledSweepAfterId, cutoff, ct);
        if (batch.Count == 0 && _settledSweepAfterId != null)
        {
            // The last page was past every remaining row. Start over so an
            // orphan inserted behind the cursor is not skipped forever.
            _settledSweepAfterId = null;
            batch = await PageSettledPartsAsync(null, cutoff, ct);
        }

        if (batch.Count == 0)
            return;

        _settledSweepAfterId = batch[^1].Id;
        var seen = new HashSet<(Guid RunId, string Domain, string GenerationId)>();
        foreach (var part in batch)
        {
            ct.ThrowIfCancellationRequested();
            var key = (part.RunId, part.Domain, part.GenerationId);
            if (!seen.Add(key))
                continue;

            if (_liveSettledConfirmedAt.TryGetValue(key, out var confirmedAt)
                && DateTimeOffset.UtcNow - confirmedAt < SweepInterval)
            {
                continue;
            }

            var header = await ReadPartitionHeaderAsync(part.RunId, part.Domain, ct);
            if (header != null
                && string.Equals(header.GenerationId, part.GenerationId, StringComparison.Ordinal))
            {
                _liveSettledConfirmedAt[key] = DateTimeOffset.UtcNow;
                AdvanceSettledCursorPastGeneration(part);
                continue;
            }

            _liveSettledConfirmedAt.Remove(key);

            var freshId = await _parts.Find(p => p.RunId == part.RunId
                    && p.Domain == part.Domain
                    && p.GenerationId == part.GenerationId
                    && p.UpdatedAt >= cutoff)
                .Limit(1)
                .Project(p => p.Id)
                .FirstOrDefaultAsync(ct);
            if (!string.IsNullOrEmpty(freshId))
                continue;

            await CosmosThrottle.ExecuteAsync(
                token => _parts.DeleteManyAsync(
                    p => p.RunId == part.RunId
                        && p.Domain == part.Domain
                        && p.GenerationId == part.GenerationId
                        && p.Settled
                        && p.UpdatedAt < cutoff,
                    token),
                ct,
                _logger);
        }
    }

    private Task<List<PartMeta>> PageSettledPartsAsync(string? afterId, DateTimeOffset cutoff, CancellationToken ct)
    {
        var filter = Builders<SnapshotPartDocument>.Filter.And(
            Builders<SnapshotPartDocument>.Filter.Eq(p => p.Settled, true),
            Builders<SnapshotPartDocument>.Filter.Lt(p => p.UpdatedAt, cutoff));
        if (afterId != null)
        {
            filter &= Builders<SnapshotPartDocument>.Filter.Gt(p => p.Id, afterId);
        }

        return CosmosThrottle.ExecuteAsync(
            token => _parts.Find(filter)
                .SortBy(p => p.Id)
                .Limit(BatchSize)
                .Project(p => new PartMeta
                {
                    Id = p.Id,
                    RunId = p.RunId,
                    Domain = p.Domain,
                    GenerationId = p.GenerationId,
                    Ordinal = p.Ordinal,
                    UpdatedAt = p.UpdatedAt
                })
                .ToListAsync(token),
            ct,
            _logger);
    }

    private async Task BackfillMissingSettledAsync(CancellationToken ct)
    {
        if (_settledBackfillComplete)
            return;

        var missing = await CosmosThrottle.ExecuteAsync(
            token => _parts.Find(Builders<SnapshotPartDocument>.Filter.Exists(p => p.Settled, false))
                .Limit(BatchSize)
                .Project(p => new PartMeta
                {
                    Id = p.Id,
                    RunId = p.RunId,
                    Domain = p.Domain,
                    GenerationId = p.GenerationId,
                    Ordinal = p.Ordinal,
                    UpdatedAt = p.UpdatedAt
                })
                .ToListAsync(token),
            ct,
            _logger);
        if (missing.Count == 0)
        {
            _settledBackfillComplete = true;
            return;
        }

        var seen = new HashSet<(Guid RunId, string Domain, string GenerationId)>();
        foreach (var part in missing)
        {
            ct.ThrowIfCancellationRequested();
            var key = (part.RunId, part.Domain, part.GenerationId);
            if (!seen.Add(key))
                continue;

            var header = await ReadPartitionHeaderAsync(part.RunId, part.Domain, ct);
            if (!IsOrphanPart(header, part))
            {
                await MarkGenerationSettledAsync(part, ct);
                continue;
            }

            // Give the field a value so the next pass does not select this
            // generation again. The aged-part query deletes real orphans.
            await CosmosThrottle.ExecuteAsync(
                token => _parts.UpdateManyAsync(
                    p => p.RunId == part.RunId && p.Domain == part.Domain && p.GenerationId == part.GenerationId,
                    Builders<SnapshotPartDocument>.Update.Set(p => p.Settled, false),
                    cancellationToken: token),
                ct,
                _logger);
        }
    }

    private async Task MarkSettledOnceAsync(
        HashSet<(Guid RunId, string Domain, string GenerationId)> marked,
        PartMeta part,
        CancellationToken ct)
    {
        if (marked.Add((part.RunId, part.Domain, part.GenerationId)))
            await MarkGenerationSettledAsync(part, ct);
    }

    private Task MarkGenerationSettledAsync(PartMeta part, CancellationToken ct)
        => CosmosThrottle.ExecuteAsync(
            token => _parts.UpdateManyAsync(
                p => p.RunId == part.RunId && p.Domain == part.Domain && p.GenerationId == part.GenerationId,
                Builders<SnapshotPartDocument>.Update.Set(p => p.Settled, true),
                cancellationToken: token),
            ct,
            _logger);

    private async Task<SnapshotPartitionHeader?> ReadPartitionHeaderAsync(Guid runId, string domain, CancellationToken ct)
    {
        PartitionHeaderReads++;
        var snapshot = await _store.ReadHeaderAsync(runId, domain, ct);
        return snapshot != null && SnapshotPartitionHeader.TryRead(snapshot.Data, out var parsed)
            ? parsed
            : null;
    }

    /// <summary>
    /// Part ids are "{runId:N}:{domain}:{generationId}:{ordinal:D8}".
    /// A trailing "::" sorts after every ordinal of that generation and
    /// before the next generation id. The cursor only moves forward.
    /// </summary>
    private void AdvanceSettledCursorPastGeneration(PartMeta part)
    {
        var generationEnd = $"{part.RunId:N}:{part.Domain}:{part.GenerationId}::";
        if (_settledSweepAfterId == null
            || string.CompareOrdinal(generationEnd, _settledSweepAfterId) > 0)
        {
            _settledSweepAfterId = generationEnd;
        }
    }

    private void PruneExpiredLiveConfirmations(DateTimeOffset now)
    {
        List<(Guid RunId, string Domain, string GenerationId)>? stale = null;
        foreach (var entry in _liveSettledConfirmedAt)
        {
            if (now - entry.Value < SweepInterval)
                continue;

            stale ??= new List<(Guid, string, string)>();
            stale.Add(entry.Key);
        }

        if (stale == null)
            return;

        foreach (var key in stale)
            _liveSettledConfirmedAt.Remove(key);
    }

    private void RememberLiveGeneration(SnapshotPartitionHeader? header, PartMeta part)
    {
        if (header == null || !string.Equals(header.GenerationId, part.GenerationId, StringComparison.Ordinal))
            return;

        _liveSettledConfirmedAt[(part.RunId, part.Domain, part.GenerationId)] = DateTimeOffset.UtcNow;
    }

    private static bool IsOrphanPart(SnapshotPartitionHeader? header, PartMeta part)
        => header == null
            || !string.Equals(header.GenerationId, part.GenerationId, StringComparison.Ordinal)
            || part.Ordinal >= header.PartCount;

    private sealed class PartMeta
    {
        public string Id { get; set; } = string.Empty;
        public Guid RunId { get; set; }
        public string Domain { get; set; } = string.Empty;
        public string GenerationId { get; set; } = string.Empty;
        public int Ordinal { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
