using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
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
        var entity = await _database.LocationConfigurationRepository.GetAsync(configId);
        if (entity == null)
            throw new KeyNotFoundException($"OrganizationLocationConfiguration with ConfigId {configId} not found.");

        ApplyUpdateToEntity(entity, model);
        _database.LocationConfigurationRepository.Update(entity);
        await _database.SaveChangesAsync();

        return ProjectToModel(entity);
    }

    public async Task<List<OrganizationLocationConfigurationModel>> UpdateByFacilityIdAsync(string facilityId, UpdateOrganizationLocationConfigurationModel model)
    {
        var entities = await _database.LocationConfigurationRepository
            .FindAsync(c => c.FacilityId == facilityId);

        if (entities.Count == 0)
            throw new KeyNotFoundException($"No OrganizationLocationConfiguration found for FacilityId {facilityId}");

        // Update ALL configs for this facility (common pattern when multiples are allowed)
        foreach (var entity in entities)
        {
            ApplyUpdateToEntity(entity, model);
            _database.LocationConfigurationRepository.Update(entity);
        }

        await _database.SaveChangesAsync();

        return entities.Select(ProjectToModel).ToList();
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
        // Delete ALL configurations for this facility
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

    private void ApplyUpdateToEntity(OrganizationLocationConfiguration entity, UpdateOrganizationLocationConfigurationModel model)
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