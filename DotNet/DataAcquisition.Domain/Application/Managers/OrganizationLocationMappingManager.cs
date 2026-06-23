using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

public interface IOrganizationLocationMappingManager
{
    Task<OrganizationLocationMappingModel> CreateAsync(CreateOrganizationLocationMappingModel model);
    Task<OrganizationLocationMappingModel> UpdateByIdAsync(int locationMappingId, UpdateOrganizationLocationMappingModel model);
    Task<OrganizationLocationMappingModel> UpdateByFacilityIdAndLocationIdAsync(string facilityId, string locationId, UpdateOrganizationLocationMappingModel model);
    Task DeleteByIdAsync(int locationMappingId);
    Task DeleteByFacilityIdAsync(string facilityId);
    Task DeleteByFacilityIdAndLocationIdAsync(string facilityId, string locationId);

    Task<int> SetParentForChildrenAsync(
        string facilityId, string parentLocationId, int parentLocationMappingId, bool isOrgLocation,
        CancellationToken cancellationToken = default);
}

public class OrganizationLocationMappingManager : IOrganizationLocationMappingManager
{
    private readonly IDatabase _database;
    private readonly DataAcquisitionDbContext _dbContext;

    public OrganizationLocationMappingManager(IDatabase database, DataAcquisitionDbContext dbContext)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<OrganizationLocationMappingModel> CreateAsync(CreateOrganizationLocationMappingModel model)
    {
        var entity = new OrganizationLocationMapping
        {
            FacilityId = model.FacilityId,
            LocationId = model.LocationId,
            LocationName = model.LocationName,
            LocationAlias = model.LocationAlias,
            PartOfValue = model.PartOfValue,
            PartOfId = model.PartOfId,
            IsOrgLocation = model.IsOrgLocation,
            CreateDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow,
            IsActive = model.IsActive
        };

        await _database.LocationMappingRepository.AddAsync(entity);

        try
        {
            await _database.SaveChangesAsync();
        }
        catch
        {
            // A failed insert (e.g. a concurrent unique-constraint violation) otherwise leaves the
            // entity tracked in Added state on this scoped DbContext, so the next SaveChangesAsync
            // (e.g. the caller's recovery update) would retry the duplicate insert and fail again.
            // Detach it so callers can cleanly recover via re-read + update.
            _dbContext.Entry(entity).State = EntityState.Detached;
            throw;
        }

        return ProjectToModel(entity);
    }

    public async Task<OrganizationLocationMappingModel> UpdateByIdAsync(int locationMappingId, UpdateOrganizationLocationMappingModel model)
    {
        var entity = await _database.LocationMappingRepository.GetAsync(locationMappingId);
        if (entity == null)
            throw new KeyNotFoundException($"OrganizationLocationMapping with Id {locationMappingId} not found.");

        ApplyUpdate(entity, model);
        _database.LocationMappingRepository.Update(entity);
        await _database.SaveChangesAsync();

        return ProjectToModel(entity);
    }

    public async Task<OrganizationLocationMappingModel> UpdateByFacilityIdAndLocationIdAsync(string facilityId, string locationId, UpdateOrganizationLocationMappingModel model)
    {
        var entity = await _database.LocationMappingRepository
            .FirstOrDefaultAsync(m => m.FacilityId == facilityId && m.LocationId == locationId);

        if (entity == null)
            throw new KeyNotFoundException($"No mapping found for FacilityId {facilityId} and LocationId {locationId}");

        return await UpdateByIdAsync(entity.LocationMappingId, model);
    }

    public async Task DeleteByIdAsync(int locationMappingId)
    {
        var entity = await _database.LocationMappingRepository.GetAsync(locationMappingId);

        if (entity != null)
        {
            _database.LocationMappingRepository.Remove(entity);
            await _database.SaveChangesAsync();
        }
    }

    public async Task DeleteByFacilityIdAsync(string facilityId)
    {
        var entities = await _database.LocationMappingRepository
            .FindAsync(m => m.FacilityId == facilityId);

        if (entities.Count == 0)
            return;

        foreach (var entity in entities)
        {
            _database.LocationMappingRepository.Remove(entity);
        }

        await _database.SaveChangesAsync();
    }

    public async Task DeleteByFacilityIdAndLocationIdAsync(string facilityId, string locationId)
    {
        var entity = await _database.LocationMappingRepository
            .FirstOrDefaultAsync(m => m.FacilityId == facilityId && m.LocationId == locationId);

        if (entity != null)
        {
            _database.LocationMappingRepository.Remove(entity);
            await _database.SaveChangesAsync();
        }
    }

    public async Task<int> SetParentForChildrenAsync(
        string facilityId, string parentLocationId, int parentLocationMappingId, bool isOrgLocation, CancellationToken cancellationToken = default)
    {
        return await _dbContext.OrganizationLocationMappings
            .Where(m => m.FacilityId == facilityId
                    && m.PartOfValue == parentLocationId
                    && m.PartOfId == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.PartOfId, parentLocationMappingId)
                .SetProperty(m => m.IsOrgLocation, m => m.IsOrgLocation || isOrgLocation) //do not demote a child if it is an org location but its parent is not
                .SetProperty(m => m.ModifiedDate, DateTime.UtcNow),
            cancellationToken);
    }    

    private void ApplyUpdate(OrganizationLocationMapping entity, UpdateOrganizationLocationMappingModel model)
    {
        if (model.LocationName != null) entity.LocationName = model.LocationName;
        if (model.LocationAlias != null) entity.LocationAlias = model.LocationAlias;
        if (model.PartOfValue != null) entity.PartOfValue = model.PartOfValue;
        if (model.PartOfId.HasValue) entity.PartOfId = model.PartOfId.Value;
        if (model.IsOrgLocation.HasValue) entity.IsOrgLocation = model.IsOrgLocation.Value;
        if (model.IsActive.HasValue) entity.IsActive = model.IsActive.Value;

        entity.ModifiedDate = DateTime.UtcNow;
    }

    private static OrganizationLocationMappingModel ProjectToModel(OrganizationLocationMapping entity)
    {
        return new OrganizationLocationMappingModel
        {
            LocationMappingId = entity.LocationMappingId,
            FacilityId = entity.FacilityId,
            LocationId = entity.LocationId,
            LocationName = entity.LocationName,
            LocationAlias = entity.LocationAlias,
            PartOfValue = entity.PartOfValue,
            PartOfId = entity.PartOfId,
            IsOrgLocation = entity.IsOrgLocation,
            CreateDate = entity.CreateDate,
            ModifiedDate = entity.ModifiedDate,
            IsActive = entity.IsActive
        };
    }
}
