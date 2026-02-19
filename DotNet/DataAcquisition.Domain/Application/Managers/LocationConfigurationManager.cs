using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

public interface ILocationConfigurationManager
{
    Task<LocationConfigurationModel> CreateAsync(CreateLocationConfigurationModel model);

    Task<LocationConfigurationModel> UpdateByIdAsync(int configId, UpdateLocationConfigurationModel model);

    Task<LocationConfigurationModel> UpdateByFacilityIdAsync(string facilityId, UpdateLocationConfigurationModel model);

    Task DeleteByIdAsync(int configId);

    Task DeleteByFacilityIdAsync(string facilityId);
}

public class LocationConfigurationManager : ILocationConfigurationManager
{
    private readonly IDatabase _database;

    public LocationConfigurationManager(IDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<LocationConfigurationModel> CreateAsync(CreateLocationConfigurationModel model)
    {
        var entity = new LocationConfiguration
        {
            FacilityId = model.FacilityId,
            Description = model.Description,
            IsActive = model.IsActive,
            CreatedOn = DateTime.UtcNow,
            ModifiedOn = DateTime.UtcNow
        };

        foreach (var cond in model.Conditions)
        {
            entity.LocationConditions.Add(new LocationCondition
            {
                FhirPath = cond.FhirPath,
                Priority = cond.Priority,
                CreatedOn = DateTime.UtcNow,
                ModifiedOn = DateTime.UtcNow
            });
        }

        await _database.LocationConfigurationRepository.AddAsync(entity);
        await _database.SaveChangesAsync();

        return ProjectToModel(entity);
    }

    public async Task<LocationConfigurationModel> UpdateByIdAsync(int configId, UpdateLocationConfigurationModel model)
    {
        var entity = await _database.LocationConfigurationRepository.GetAsync(configId);
        if (entity == null)
            throw new KeyNotFoundException($"LocationConfiguration with ConfigId {configId} not found.");

        ApplyUpdateToEntity(entity, model);
        _database.LocationConfigurationRepository.Update(entity);
        await _database.SaveChangesAsync();

        return ProjectToModel(entity);
    }

    public async Task<LocationConfigurationModel> UpdateByFacilityIdAsync(string facilityId, UpdateLocationConfigurationModel model)
    {
        var entities = await _database.LocationConfigurationRepository
            .FindAsync(c => c.FacilityId == facilityId);

        if (entities.Count == 0)
            throw new KeyNotFoundException($"No LocationConfiguration found for FacilityId {facilityId}");

        // Update ALL configs for this facility (common pattern when multiples are allowed)
        foreach (var entity in entities)
        {
            ApplyUpdateToEntity(entity, model);
            _database.LocationConfigurationRepository.Update(entity);
        }

        await _database.SaveChangesAsync();

        // Return the first updated one (you can change this if you prefer returning a list)
        return ProjectToModel(entities.First());
    }

    public async Task DeleteByIdAsync(int configId)
    {
        var entity = await _database.LocationConfigurationRepository.GetAsync(configId);
        if (entity != null)
        {
            _database.LocationConfigurationRepository.Remove(entity);
            await _database.SaveChangesAsync();
        }
    }

    public async Task DeleteByFacilityIdAsync(string facilityId)
    {
        // IMPORTANT: Delete ALL configurations for this facility
        var entities = await _database.LocationConfigurationRepository
            .FindAsync(c => c.FacilityId == facilityId);

        if (entities.Count == 0)
            return; // Nothing to delete

        foreach (var entity in entities)
        {
            _database.LocationConfigurationRepository.Remove(entity);
        }

        await _database.SaveChangesAsync();
    }

    // Helper to avoid code duplication for update logic
    private void ApplyUpdateToEntity(LocationConfiguration entity, UpdateLocationConfigurationModel model)
    {
        if (model.Description != null)
            entity.Description = model.Description;

        if (model.IsActive.HasValue)
            entity.IsActive = model.IsActive.Value;

        entity.ModifiedOn = DateTime.UtcNow;

        if (model.Conditions != null)
        {
            // Replace all conditions
            foreach (var cond in entity.LocationConditions.ToList())
            {
                _database.LocationConditionRepository.Remove(cond);
            }
            entity.LocationConditions.Clear();

            foreach (var cond in model.Conditions)
            {
                entity.LocationConditions.Add(new LocationCondition
                {
                    FhirPath = cond.FhirPath,
                    Priority = cond.Priority,
                    CreatedOn = DateTime.UtcNow,
                    ModifiedOn = DateTime.UtcNow
                });
            }
        }
    }

    // Private projection (EF entity never leaves this class)
    private static LocationConfigurationModel ProjectToModel(LocationConfiguration entity)
    {
        return new LocationConfigurationModel
        {
            ConfigId = entity.ConfigId,
            FacilityId = entity.FacilityId,
            Description = entity.Description,
            IsActive = entity.IsActive ?? false,
            CreatedOn = entity.CreatedOn ?? DateTime.UtcNow,
            ModifiedOn = entity.ModifiedOn ?? DateTime.UtcNow,
            Conditions = entity.LocationConditions.Select(c => new LocationConditionModel
            {
                ConditionId = c.ConditionId,
                FhirPath = c.FhirPath,
                Priority = c.Priority ?? 1,
                CreatedOn = c.CreatedOn ?? DateTime.UtcNow,
                ModifiedOn = c.ModifiedOn ?? DateTime.UtcNow
            }).ToList()
        };
    }
}