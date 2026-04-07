using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

public interface IReferenceResourcesManager
{
    Task<ReferenceResourcesModel> CreateAsync(CreateReferenceResourcesModel model, CancellationToken cancellationToken = default);
    Task<ReferenceResourcesModel> UpdateAsync(UpdateReferenceResourcesModel model, CancellationToken cancellationToken = default);
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
        activity?.SetTag(DiagnosticNames.ReportId, model.DataAcquisitionLogId);
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
            DataAcquisitionLogId = model.DataAcquisitionLogId,
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

        var updated = await _dbContext.ReferenceResources
            .Where(r => r.Id == model.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.QueryPhase, model.QueryPhase)
                .SetProperty(r => r.ResourceType, model.ResourceType)
                .SetProperty(r => r.ReferenceResource, model.ReferenceResource)
                .SetProperty(r => r.ModifyDate, DateTime.UtcNow),
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
            ReferenceResource = model.ReferenceResource
        };
    }
}