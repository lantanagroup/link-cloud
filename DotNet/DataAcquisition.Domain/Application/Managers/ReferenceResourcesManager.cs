using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System.Data;
using IDatabase = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.IDatabase;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

public interface IReferenceResourcesManager
{
    Task CreateBatchAsync(IReadOnlyList<CreateReferenceResourcesModel> models, CancellationToken cancellationToken = default);

    Task LinkToLogAsync(long dataAcquisitionLogId, IReadOnlyList<Guid> referenceResourceIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stage discovered reference resource ids onto the <c>PendingReferenceIds</c> table for
    /// later promotion into a referential-phase <see cref="DataAcquisitionLog"/>.
    /// Deduplicates against existing staging rows and is safe to call concurrently from
    /// multiple primary-phase workers; the unique index on the staging table is the
    /// authoritative dedupe, and this method swallows the narrow race-window conflicts.
    /// </summary>
    Task StagePendingReferencesAsync(
        string facilityId,
        string correlationId,
        IReadOnlyList<(string ResourceType, string ResourceId)> references,
        CancellationToken cancellationToken = default);
}

public class ReferenceResourcesManager : IReferenceResourcesManager
{
    private const int InsertChunkSize = 250;
    private const int LockTimeoutMs = 30000;
    private const int ExistenceCheckChunkSize = 800;

    private readonly ILogger<ReferenceResourcesManager> _logger;
    private readonly IDatabase _database;
    private readonly DataAcquisitionDbContext _dbContext;

    public ReferenceResourcesManager(ILogger<ReferenceResourcesManager> logger, IDatabase database, DataAcquisitionDbContext dbContext)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task CreateBatchAsync(IReadOnlyList<CreateReferenceResourcesModel> models, CancellationToken cancellationToken = default)
    {
        if (models == null || models.Count == 0)
            return;

        var dedupedModels = models
            .GroupBy(m => new { m.FacilityId, m.ResourceType, m.ResourceId })
            .Select(g => g.First())
            .ToList();

        foreach (var group in dedupedModels.GroupBy(m => new { m.FacilityId, m.ResourceType }))
        {
            var facilityId = group.Key.FacilityId;
            var resourceType = group.Key.ResourceType;
            var batch = group.ToList();

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

            await AcquireLockAsync($"ReferenceResources:{facilityId}:{resourceType}", cancellationToken);

            try
            {
                var now = DateTime.UtcNow;
                var resourceIds = batch.Select(m => m.ResourceId).Distinct().ToList();

                var existingKeys = await GetExistingResourceIdsAsync(facilityId, resourceType, resourceIds, cancellationToken);

                var toInsert = batch
                    .Where(m => !existingKeys.Contains(m.ResourceId))
                    .Select(m => new ReferenceResources
                    {
                        FacilityId = m.FacilityId,
                        ResourceId = m.ResourceId,
                        ResourceType = m.ResourceType,
                        ReferenceResource = m.ReferenceResource,
                        QueryPhase = m.QueryPhase,
                        CreateDate = now,
                        ModifyDate = now
                    })
                    .ToList();

                if (toInsert.Count > 0)
                {
                    foreach (var chunk in Chunk(toInsert, InsertChunkSize))
                    {
                        _dbContext.ReferenceResources.AddRange(chunk);
                        await _dbContext.SaveChangesAsync(cancellationToken);
                    }
                }

                _dbContext.ChangeTracker.Clear();

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }

    private async Task<HashSet<string>> GetExistingResourceIdsAsync(string facilityId, string resourceType, IReadOnlyList<string> resourceIds, CancellationToken cancellationToken)
    {
        var existingKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var idChunk in Chunk(resourceIds, ExistenceCheckChunkSize))
        {
            var existing = await _dbContext.ReferenceResources
                .Where(r => r.FacilityId == facilityId
                         && r.ResourceType == resourceType
                         && idChunk.Contains(r.ResourceId))
                .Select(r => r.ResourceId)
                .ToListAsync(cancellationToken);

            foreach (var id in existing)
                existingKeys.Add(id);
        }

        return existingKeys;
    }

    private async Task AcquireLockAsync(string resource, CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();

        command.CommandText = @"
            DECLARE @result int;
            EXEC @result = sp_getapplock
                @Resource = @resource,
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = @lockTimeoutMs,
                @DbPrincipal = 'public';
            SELECT @result;";

        var resourceParam = command.CreateParameter();
        resourceParam.ParameterName = "@resource";
        resourceParam.Value = resource;
        command.Parameters.Add(resourceParam);

        var timeoutParam = command.CreateParameter();
        timeoutParam.ParameterName = "@lockTimeoutMs";
        timeoutParam.Value = LockTimeoutMs;
        command.Parameters.Add(timeoutParam);

        var dbTransaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction();
        if (dbTransaction == null)
            throw new InvalidOperationException("AcquireLockAsync must be called within an active transaction.");

        command.Transaction = dbTransaction;

        var resultObj = await command.ExecuteScalarAsync(cancellationToken);
        var result = Convert.ToInt32(resultObj);

        if (result < 0)
            throw new InvalidOperationException($"Unable to acquire SQL app lock for resource '{resource}'. sp_getapplock result={result}.");
    }

    private static IEnumerable<List<T>> Chunk<T>(IReadOnlyList<T> source, int size)
    {
        for (var i = 0; i < source.Count; i += size)
            yield return source.Skip(i).Take(size).ToList();
    }

    public async Task LinkToLogAsync(long dataAcquisitionLogId, IReadOnlyList<Guid> referenceResourceIds, CancellationToken cancellationToken = default)
    {
        if (referenceResourceIds == null || referenceResourceIds.Count == 0)
            return;

        var log = await _dbContext.DataAcquisitionLogs
            .Include(l => l.ReferenceResources)
            .FirstOrDefaultAsync(l => l.Id == dataAcquisitionLogId, cancellationToken);

        if (log == null)
            return;

        var existingIds = log.ReferenceResources.Select(r => r.Id).ToHashSet();
        var newIds = referenceResourceIds.Where(id => !existingIds.Contains(id)).ToList();

        if (newIds.Count > 0)
        {
            var resourcesToLink = await _dbContext.ReferenceResources
                .Where(r => newIds.Contains(r.Id))
                .ToListAsync(cancellationToken);

            foreach (var resource in resourcesToLink)
                log.ReferenceResources.Add(resource);

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task StagePendingReferencesAsync(
        string facilityId,
        string correlationId,
        IReadOnlyList<(string ResourceType, string ResourceId)> references,
        CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("ReferenceResourcesManager.StagePendingReferencesAsync");
        activity?.SetTag(DiagnosticNames.FacilityId, facilityId);
        activity?.SetTag(DiagnosticNames.CorrelationId, correlationId);

        if (string.IsNullOrWhiteSpace(facilityId))
            throw new ArgumentException("FacilityId is required.", nameof(facilityId));
        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentException("CorrelationId is required.", nameof(correlationId));
        if (references == null || references.Count == 0)
            return;

        // In-memory dedupe of the input batch.
        var deduped = references
            .Where(r => !string.IsNullOrWhiteSpace(r.ResourceType) && !string.IsNullOrWhiteSpace(r.ResourceId))
            .Distinct()
            .ToList();

        if (deduped.Count == 0)
            return;

        // Pre-read existing staging rows for this correlation so we skip the vast majority
        // of inserts in the steady-state case. The unique index on
        // (FacilityId, CorrelationId, ResourceType, ResourceId) is the authoritative
        // dedupe; this read is an optimization.
        var resourceIds = deduped.Select(r => r.ResourceId).Distinct().ToList();
        var existing = new HashSet<(string Type, string Id)>();

        foreach (var idChunk in Chunk(resourceIds, ExistenceCheckChunkSize))
        {
            var rows = await _dbContext.PendingReferenceIds
                .AsNoTracking()
                .Where(p => p.FacilityId == facilityId
                         && p.CorrelationId == correlationId
                         && idChunk.Contains(p.ResourceId))
                .Select(p => new { p.ResourceType, p.ResourceId })
                .ToListAsync(cancellationToken);

            foreach (var r in rows)
                existing.Add((r.ResourceType, r.ResourceId));
        }

        var toInsert = deduped
            .Where(r => !existing.Contains((r.ResourceType, r.ResourceId)))
            .Select(r => new PendingReferenceId
            {
                FacilityId = facilityId,
                CorrelationId = correlationId,
                ResourceType = r.ResourceType,
                ResourceId = r.ResourceId,
                CreateDate = DateTime.UtcNow
            })
            .ToList();

        if (toInsert.Count == 0)
            return;

        foreach (var chunk in Chunk(toInsert, InsertChunkSize))
        {
            try
            {
                _dbContext.PendingReferenceIds.AddRange(chunk);
                await _dbContext.SaveChangesAsync(cancellationToken);
                _dbContext.ChangeTracker.Clear();
            }
            catch (DbUpdateException batchEx)
            {
                // A concurrent stager inserted overlapping rows between our pre-read
                // and our write, violating the unique index. Fall back to row-by-row
                // inserts, swallowing only unique-violation conflicts so surviving
                // rows still land.
                _dbContext.ChangeTracker.Clear();
                _logger.LogDebug(batchEx,
                    "StagePendingReferencesAsync: batch insert collided with a concurrent stager for correlation {CorrelationId}; falling back to per-row inserts.",
                    correlationId);

                foreach (var row in chunk)
                {
                    try
                    {
                        _dbContext.PendingReferenceIds.Add(row);
                        await _dbContext.SaveChangesAsync(cancellationToken);
                    }
                    catch (DbUpdateException rowEx)
                    {
                        // Duplicate already present — safe to skip. Log at debug so noise
                        // stays low but races are still diagnosable.
                        _logger.LogDebug(rowEx,
                            "StagePendingReferencesAsync: skipping duplicate pending reference {ResourceType}/{ResourceId} for correlation {CorrelationId}.",
                            row.ResourceType, row.ResourceId, correlationId);
                    }
                    finally
                    {
                        _dbContext.ChangeTracker.Clear();
                    }
                }
            }
        }
    }
}