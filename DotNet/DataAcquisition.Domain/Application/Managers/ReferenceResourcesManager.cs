using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

public interface IReferenceResourcesManager
{
    Task<ReferenceResourcesModel> CreateAsync(CreateReferenceResourcesModel model, CancellationToken cancellationToken = default);
    Task<ReferenceResourcesModel> UpdateAsync(UpdateReferenceResourcesModel model, CancellationToken cancellationToken = default);
    Task CreateBatchAsync(IReadOnlyList<CreateReferenceResourcesModel> models, CancellationToken cancellationToken = default);
    Task UpdateBatchAsync(IReadOnlyList<UpdateReferenceResourcesModel> models, CancellationToken cancellationToken = default);

    /// <summary>
    /// Links existing canonical reference resource rows to a data acquisition log
    /// via the junction table. Duplicate links are silently ignored.
    /// </summary>
    Task LinkToLogAsync(long dataAcquisitionLogId, IReadOnlyList<Guid> referenceResourceIds, CancellationToken cancellationToken = default);
}

public class ReferenceResourcesManager : IReferenceResourcesManager
{
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

        if (model == null)
        {
            throw new ArgumentNullException(nameof(model));
        }

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

        if (model == null)
        {
            throw new ArgumentNullException(nameof(model));
        }

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
        {
            throw new KeyNotFoundException($"ReferenceResources with ID {model.Id} not found.");
        }

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

        var now = DateTime.UtcNow;
        var entities = models.Select(m => new ReferenceResources
        {
            FacilityId = m.FacilityId,
            ResourceId = m.ResourceId,
            ResourceType = m.ResourceType,
            ReferenceResource = m.ReferenceResource,
            QueryPhase = m.QueryPhase,
            CreateDate = now,
            ModifyDate = now
        }).ToList();

        _dbContext.ReferenceResources.AddRange(entities);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Concurrent patient workers raced to insert the same canonical
            // reference resource. The winner already inserted it; detach the
            // duplicates so the DbContext is clean for subsequent operations.
            _logger.LogDebug("Duplicate reference resource insert detected during CreateBatchAsync; treating as idempotent race.");

            foreach (var entry in _dbContext.ChangeTracker.Entries<ReferenceResources>()
                         .Where(e => e.State == EntityState.Added))
            {
                entry.State = EntityState.Detached;
            }
        }
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
            {
                log.ReferenceResources.Add(resource);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}