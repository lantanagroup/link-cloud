using Hl7.FhirPath;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Interfaces;

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
    private readonly IOrganizationLocationConfigurationQueries _organizationLocationConfigurationQueries;
    private readonly ICacheService _cacheService;

    public OrganizationLocationConfigurationManager(IDatabase database, IOrganizationLocationConfigurationQueries organizationLocationConfigurationQueries, ICacheService cacheService)
    {
        _database = database;
        _organizationLocationConfigurationQueries = organizationLocationConfigurationQueries;
        _cacheService = cacheService;
    }

    // The active-conditions read cache (populated by LocationMappingService) must be invalidated
    // whenever a facility's configuration/conditions change, otherwise the acquire path keeps
    // evaluating stale conditions for up to the cache TTL.
    private void InvalidateConditionsCache(string facilityId) =>
        _cacheService.Remove(OrgLocationCacheKeys.Conditions(facilityId));

    public async Task<OrganizationLocationConfigurationModel> CreateAsync(CreateOrganizationLocationConfigurationModel model)
    {
        ValidateConditions(model.Conditions.Select(c => c.FhirPath));

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

        InvalidateConditionsCache(entity.FacilityId);

        return ProjectToModel(entity);
    }

    public async Task<OrganizationLocationConfigurationModel> UpdateByIdAsync(int configId, UpdateOrganizationLocationConfigurationModel model)
    {
        OrganizationLocationConfigurationModel result = null;

        await _database.ExecuteInTransactionAsync(async () =>
        {
            var entity = await _database.LocationConfigurationRepository.GetAsync(configId);

            if (entity == null)
                throw new NotFoundException($"OrganizationLocationConfiguration with ConfigId {configId} not found.");

            entity.LocationConditions =
                await _database.LocationConditionRepository.FindAsync(c => c.ConfigId == configId);

            await ApplyUpdateToEntity(entity, model);
            _database.LocationConfigurationRepository.Update(entity);

            await _database.SaveChangesAsync();

            InvalidateConditionsCache(entity.FacilityId);

            result = ProjectToModel(entity);
        });

        return result;
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
        await _database.ExecuteInTransactionAsync(async () =>
        {
            var entity = await _database.LocationConfigurationRepository.GetAsync(configId);

            entity.LocationConditions =
                await _database.LocationConditionRepository.FindAsync(c => c.ConfigId == configId);

            foreach (var condition in entity.LocationConditions)
            {
                _database.LocationConditionRepository.Remove(condition);
            }

            _database.LocationConfigurationRepository.Remove(entity);
            await _database.SaveChangesAsync();

            InvalidateConditionsCache(entity.FacilityId);
        });
    }

    public async Task DeleteByFacilityIdAsync(string facilityId)
    {
        var entities = await _database.LocationConfigurationRepository
            .FindAsync(c => c.FacilityId == facilityId);

        foreach (var entity in entities)
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
            ValidateConditions(model.Conditions.Select(c => c.FhirPath));

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

    private static void ValidateConditions(IEnumerable<string> fhirPaths)
    {
        foreach (var fhirPath in fhirPaths)
        {
            if (string.IsNullOrWhiteSpace(fhirPath))
                continue;

            var error = TryGetFhirPathError(fhirPath);
            if (error != null)
                throw new BadRequestException($"Invalid FHIRPath syntax: {error}");
        }
    }

    /// <summary>
    /// Compiles the expression with Firely's FhirPathCompiler; returns null when it
    /// compiles, otherwise the compiler's error message.
    /// </summary>
    private static string? TryGetFhirPathError(string fhirPath)
    {
        try
        {
            new FhirPathCompiler().Compile(fhirPath);
            return null;
        }
        catch (Exception ex) when (ex is FormatException || ex is ArgumentException)
        {
            return ex.Message;
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