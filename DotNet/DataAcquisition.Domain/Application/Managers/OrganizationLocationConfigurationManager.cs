using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

public interface IOrganizationLocationConfigurationManager
{
    Task<OrganizationLocationConfigurationModel> CreateAsync(CreateOrganizationLocationConfigurationModel model);

    Task<OrganizationLocationConfigurationModel> UpdateByIdAsync(int configId, UpdateOrganizationLocationConfigurationModel model);

    Task<List<OrganizationLocationConfigurationModel>> UpdateByFacilityIdAsync(string facilityId, UpdateOrganizationLocationConfigurationModel model);

    Task DeleteByIdAsync(int configId);

    Task DeleteByFacilityIdAsync(string facilityId);
}

public class OrganizationLocationConfigurationManager : IOrganizationLocationConfigurationManager
{
    private readonly IDatabase _database;

    public OrganizationLocationConfigurationManager(IDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<OrganizationLocationConfigurationModel> CreateAsync(CreateOrganizationLocationConfigurationModel model)
    {
        var entity = new OrganizationLocationConfiguration
        {
            FacilityId = model.FacilityId,
            Description = model.Description,
            IsActive = model.IsActive,
            CreatedOn = DateTime.UtcNow,
            ModifiedOn = DateTime.UtcNow
        };

        foreach (var cond in model.Conditions)
        {
            entity.LocationConditions.Add(new OrganizationLocationCondition
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

    public async Task<OrganizationLocationConfigurationModel> UpdateByIdAsync(int configId, UpdateOrganizationLocationConfigurationModel model)
    {
        try
        {
            await _database.BeginTransactionAsync();
            var entity = await _database.LocationConfigurationRepository.GetAsync(configId);
            entity.LocationConditions = await _database.LocationConditionRepository.FindAsync(c => c.ConfigId == configId);

            if (entity == null)
                throw new KeyNotFoundException($"OrganizationLocationConfiguration with ConfigId {configId} not found.");

            await ApplyUpdateToEntity(entity, model);
            _database.LocationConfigurationRepository.Update(entity);

            await _database.SaveChangesAsync();

            await _database.CommitTransactionAsync();

            return ProjectToModel(entity);
        }
        catch
        {
            await _database.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<List<OrganizationLocationConfigurationModel>> UpdateByFacilityIdAsync(string facilityId, UpdateOrganizationLocationConfigurationModel model)
    {
        var entities = await _database.LocationConfigurationRepository
            .FindAsync(c => c.FacilityId == facilityId);

        if (entities.Count == 0)
            throw new KeyNotFoundException($"No OrganizationLocationConfiguration found for FacilityId {facilityId}");

        var updatedEntities = new List<OrganizationLocationConfigurationModel>();
        foreach (var entity in entities)
        {
            var updated = await UpdateByIdAsync(entity.ConfigId, model);

            updatedEntities.Add(updated);
        }

        return updatedEntities;
    }

    public async Task DeleteByIdAsync(int configId)
    {
        try
        {
            await _database.BeginTransactionAsync();

            var entity = await _database.LocationConfigurationRepository.GetAsync(configId);

            entity.LocationConditions = await _database.LocationConditionRepository.FindAsync(c => c.ConfigId == configId);

            foreach (var condition in entity.LocationConditions)
            {
                _database.LocationConditionRepository.Remove(condition);
            }

            _database.LocationConfigurationRepository.Remove(entity);
            await _database.SaveChangesAsync();
            
            await _database.CommitTransactionAsync();
        }
        catch
        {
            await _database.RollbackTransactionAsync();
        }
    }

    public async Task DeleteByFacilityIdAsync(string facilityId)
    {
        // Delete ALL configurations for this facility
        var entities = await _database.LocationConfigurationRepository
            .FindAsync(c => c.FacilityId == facilityId);

        foreach(var entity in entities)
        {
            await DeleteByIdAsync(entity.ConfigId);
        }
    }

    private async Task ApplyUpdateToEntity(OrganizationLocationConfiguration entity, UpdateOrganizationLocationConfigurationModel model)
    {
        if (model.Description != null)
            entity.Description = model.Description;

        if (model.IsActive.HasValue)
            entity.IsActive = model.IsActive.Value;

        entity.ModifiedOn = DateTime.UtcNow;

        if (model.Conditions != null && model.Conditions.Any())
        {
            //Rebuild conditions
            foreach (var condition in entity.LocationConditions)
            {
                _database.LocationConditionRepository.Remove(condition);
            }

            entity.LocationConditions.Clear();

            await _database.SaveChangesAsync();

            foreach (var cond in model.Conditions)
            {
                entity.LocationConditions.Add(new OrganizationLocationCondition
                {
                    FhirPath = cond.FhirPath,
                    Priority = cond.Priority,
                    CreatedOn = DateTime.UtcNow,
                    ModifiedOn = DateTime.UtcNow
                });
            }
        }
    }

    private static OrganizationLocationConfigurationModel ProjectToModel(OrganizationLocationConfiguration entity)
    {
        return new OrganizationLocationConfigurationModel
        {
            ConfigId = entity.ConfigId,
            FacilityId = entity.FacilityId,
            Description = entity.Description,
            IsActive = entity.IsActive,
            CreatedOn = entity.CreatedOn ?? DateTime.UtcNow,
            ModifiedOn = entity.ModifiedOn ?? DateTime.UtcNow,
            Conditions = entity.LocationConditions.Select(c => new OrganizationLocationConditionModel
            {
                ConditionId = c.ConditionId,
                FhirPath = c.FhirPath,
                Priority = c.Priority,
                CreatedOn = c.CreatedOn ?? DateTime.UtcNow,
                ModifiedOn = c.ModifiedOn ?? DateTime.UtcNow
            }).ToList()
        };
    }
}