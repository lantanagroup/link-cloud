using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using Microsoft.Extensions.Logging;

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

    public ReferenceResourcesManager(ILogger<ReferenceResourcesManager> logger, IDatabase database)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<ReferenceResourcesModel> CreateAsync(CreateReferenceResourcesModel model, CancellationToken cancellationToken = default)
    {
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
        if (model == null)
        {
            throw new ArgumentNullException(nameof(model));
        }

        var existing = await _database.ReferenceResourcesRepository.FirstOrDefaultAsync(x => x.Id == model.Id, cancellationToken);
        if (existing == null)
        {
            throw new KeyNotFoundException($"ReferenceResources with ID {model.Id} not found.");
        }

        existing.QueryPhase = model.QueryPhase;
        existing.ResourceType = model.ResourceType;
        existing.ReferenceResource = model.ReferenceResource;
        existing.ModifyDate = DateTime.UtcNow;

        await _database.ReferenceResourcesRepository.SaveChangesAsync(cancellationToken);

        return ReferenceResourcesModel.FromDomain(existing);
    }
}