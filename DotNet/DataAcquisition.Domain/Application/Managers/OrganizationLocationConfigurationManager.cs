using Hl7.FhirPath;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Validators;
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
    private readonly IOrganizationLocationConfigurationQueries _organizationLocationConfigurationQueries;
    private readonly ILocationResolutionValidator _locationResolutionValidator;

    public OrganizationLocationConfigurationManager(
        IDatabase database,
        IOrganizationLocationConfigurationQueries organizationLocationConfigurationQueries,
        ILocationResolutionValidator locationResolutionValidator)
    {
        _database = database;
        _organizationLocationConfigurationQueries = organizationLocationConfigurationQueries;
        _locationResolutionValidator = locationResolutionValidator;
    }

    public async Task<OrganizationLocationConfigurationModel> CreateAsync(CreateOrganizationLocationConfigurationModel model)
    {
        ValidateConditions(model.Conditions.Select(c => c.FhirPath));

        // Activating location resolution requires every frequency plan's initial queries to
        // include both an Encounter and a Location query.
        if (model.IsActive)
        {
            await _locationResolutionValidator.ValidateActivationAsync(model.FacilityId);
        }

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
        OrganizationLocationConfigurationModel result = null;

        await _database.ExecuteInTransactionAsync(async () =>
        {
            var entity = await _database.LocationConfigurationRepository.GetAsync(configId);

            if (entity == null)
                throw new NotFoundException($"OrganizationLocationConfiguration with ConfigId {configId} not found.");

            // Activating location resolution (either turning it on now, or keeping it on) requires
            // every frequency plan's initial queries to include both an Encounter and a Location query.
            var desiredIsActive = model.IsActive ?? entity.IsActive;
            if (desiredIsActive)
            {
                await _locationResolutionValidator.ValidateActivationAsync(entity.FacilityId);
            }

            entity.LocationConditions =
                await _database.LocationConditionRepository.FindAsync(c => c.ConfigId == configId);

            await ApplyUpdateToEntity(entity, model);
            _database.LocationConfigurationRepository.Update(entity);

            await _database.SaveChangesAsync();

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