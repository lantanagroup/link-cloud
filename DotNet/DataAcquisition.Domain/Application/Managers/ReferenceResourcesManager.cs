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
    Task<ReferenceResourcesModel> CreateAsync(CreateReferenceResourcesModel model, CancellationToken cancellationToken = default);
    Task<ReferenceResourcesModel> UpdateAsync(UpdateReferenceResourcesModel model, CancellationToken cancellationToken = default);
    Task CreateBatchAsync(IReadOnlyList<CreateReferenceResourcesModel> models, CancellationToken cancellationToken = default);
    Task UpdateBatchAsync(IReadOnlyList<UpdateReferenceResourcesModel> models, CancellationToken cancellationToken = default);

    Task LinkToLogAsync(long dataAcquisitionLogId, IReadOnlyList<Guid> referenceResourceIds, CancellationToken cancellationToken = default);
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

    public async Task<ReferenceResourcesModel> CreateAsync(CreateReferenceResourcesModel model, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("ReferenceResourcesManager.CreateAsync");
        activity?.SetTag(DiagnosticNames.FacilityId, model.FacilityId);
        activity?.SetTag(DiagnosticNames.ResourceId, model.ResourceId);
        activity?.SetTag(DiagnosticNames.ResourceType, model.ResourceType);

        if (model == null) throw new ArgumentNullException(nameof(model));

        var entity = new ReferenceResources
        {
            FacilityId = model.FacilityId,
            ResourceId = model.ResourceId,
            ResourceType = model.ResourceType,
            ReferenceResource = model.ReferenceResource,
            QueryPhase = model.QueryPhase,
            CreateDate = DateTime.UtcNow,
            ModifyDate = DateTime.UtcNow
        };

        entity = await _database.ReferenceResourcesRepository.AddAsync(entity);
        await _database.ReferenceResourcesRepository.SaveChangesAsync(cancellationToken);

        return ReferenceResourcesModel.FromDomain(entity);
    }

    public async Task<ReferenceResourcesModel> UpdateAsync(UpdateReferenceResourcesModel model, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("ReferenceResourcesManager.UpdateAsync");
        activity?.SetTag(DiagnosticNames.ResourceId, model.Id);
        activity?.SetTag(DiagnosticNames.ResourceType, model.ResourceType);

        if (model == null) throw new ArgumentNullException(nameof(model));

        var modifyDate = DateTime.UtcNow;

        var updated = await _dbContext.ReferenceResources
            .Where(r => r.Id == model.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.QueryPhase, model.QueryPhase)
                .SetProperty(r => r.ResourceType, model.ResourceType)
                .SetProperty(r => r.ReferenceResource, model.ReferenceResource)
                .SetProperty(r => r.ModifyDate, modifyDate),
            cancellationToken);

        if (updated == 0)
            throw new KeyNotFoundException($"ReferenceResources with ID {model.Id} not found.");

        return new ReferenceResourcesModel
        {
            Id = model.Id,
            QueryPhase = model.QueryPhase,
            ResourceType = model.ResourceType,
            ReferenceResource = model.ReferenceResource,
            ModifyDate = modifyDate
        };
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

    public async Task UpdateBatchAsync(IReadOnlyList<UpdateReferenceResourcesModel> models, CancellationToken cancellationToken = default)
    {
        if (models == null || models.Count == 0)
            return;

        var modifyDate = DateTime.UtcNow;

        foreach (var model in models)
        {
            await _dbContext.ReferenceResources
                .Where(r => r.Id == model.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(r => r.QueryPhase, model.QueryPhase)
                    .SetProperty(r => r.ResourceType, model.ResourceType)
                    .SetProperty(r => r.ReferenceResource, model.ReferenceResource)
                    .SetProperty(r => r.ModifyDate, modifyDate),
                cancellationToken);
        }
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