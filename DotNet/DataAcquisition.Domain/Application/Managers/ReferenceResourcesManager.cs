using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

public interface IReferenceResourcesManager
{
    Task CreateBatchAsync(IReadOnlyList<CreateReferenceResourcesModel> models, CancellationToken cancellationToken = default);

    Task LinkToLogAsync(long dataAcquisitionLogId, IReadOnlyList<Guid> referenceResourceIds, CancellationToken cancellationToken = default);
}

public class ReferenceResourcesManager : IReferenceResourcesManager
{
    private const int InsertChunkSize = 250;
    private const int LockTimeoutMs = 30000;
    private const int ExistenceCheckChunkSize = 800;

    private readonly DataAcquisitionDbContext _dbContext;

    public ReferenceResourcesManager(DataAcquisitionDbContext dbContext)
    {
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

            var strategy = _dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async (ct) =>
            {
                _dbContext.ChangeTracker.Clear();

                await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

                await AcquireLockAsync($"ReferenceResources:{facilityId}:{resourceType}", ct);

                try
                {
                    var now = DateTime.UtcNow;
                    var resourceIds = batch.Select(m => m.ResourceId).Distinct().ToList();

                    var existingKeys = await GetExistingResourceIdsAsync(facilityId, resourceType, resourceIds, ct);

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
                            await _dbContext.SaveChangesAsync(ct);
                        }
                    }

                    _dbContext.ChangeTracker.Clear();

                    await transaction.CommitAsync(ct);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            }, cancellationToken);
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
}