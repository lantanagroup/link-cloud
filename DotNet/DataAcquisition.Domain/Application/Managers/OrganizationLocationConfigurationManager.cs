using Hl7.FhirPath;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
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
    private readonly ICacheService _cacheService;
    private readonly ILocationMappingService _locationMappingService;

    public OrganizationLocationConfigurationManager(IDatabase database, IOrganizationLocationConfigurationQueries organizationLocationConfigurationQueries, ICacheService cacheService, ILocationMappingService locationMappingService)
    {
        _database = database;
        _organizationLocationConfigurationQueries = organizationLocationConfigurationQueries;
        _locationResolutionValidator = locationResolutionValidator;
        _cacheService = cacheService;
        _locationMappingService = locationMappingService;
    }

    // The active-conditions read cache (populated by LocationMappingService) must be invalidated
    // whenever a facility's configuration/conditions change, otherwise the acquire path keeps
    // evaluating stale conditions for up to the cache TTL.
    private void InvalidateConditionsCache(string facilityId) =>
        _cacheService.Remove(OrgLocationCacheKeys.Conditions(facilityId));

    public async Task<OrganizationLocationConfigurationModel> CreateAsync(CreateOrganizationLocationConfigurationModel model, CancellationToken cancellationToken = default)
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

        InvalidateConditionsCache(entity.FacilityId);

        // Re-classify the facility's already-cached Locations against the new conditions so the next
        // report run doesn't reuse a stale IsOrgLocation/MappedToOrg. CancellationToken.None: the config
        // is already committed, so a caller cancelling now must not skip this refresh and leave it stale.
        await _locationMappingService.ReevaluateLocationMappingsAsync(entity.FacilityId, CancellationToken.None);

        return ProjectToModel(entity);
    }

    public async Task<OrganizationLocationConfigurationModel> UpdateByIdAsync(int configId, UpdateOrganizationLocationConfigurationModel model, CancellationToken cancellationToken = default)
    {
        OrganizationLocationConfigurationModel result = null;
        string? facilityId = null;

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

            InvalidateConditionsCache(entity.FacilityId);
            facilityId = entity.FacilityId;

            result = ProjectToModel(entity);
        });

        // Re-evaluate AFTER the configuration transaction commits: re-evaluation reads the committed
        // conditions and performs its own writes, so it must not run inside that transaction.
        // CancellationToken.None: the change is committed, so a caller cancelling now must not skip
        // the refresh and leave IsOrgLocation/MappedToOrg stale.
        if (facilityId != null)
        {
            await _locationMappingService.ReevaluateLocationMappingsAsync(facilityId, CancellationToken.None);
        }

        return result;
    }

    public async Task<List<OrganizationLocationConfigurationModel>> UpdateByFacilityIdAsync(string facilityId, UpdateOrganizationLocationConfigurationModel model, CancellationToken cancellationToken = default)
    {
        var updatedEntities = new List<OrganizationLocationConfigurationModel>();

        // Update every config for the facility in a single transaction, then invalidate and re-evaluate
        // once — not per config. Per-item reevaluation (via UpdateByIdAsync) would expose partially
        // updated mappings and repeat the full mapping scan for each config.
        await _database.ExecuteInTransactionAsync(async () =>
        {
            var entities = await _database.LocationConfigurationRepository
                .FindAsync(c => c.FacilityId == facilityId, cancellationToken);

            if (entities.Count == 0)
                throw new KeyNotFoundException($"No OrganizationLocationConfiguration found for FacilityId {facilityId}");

            foreach (var entity in entities)
            {
                entity.LocationConditions =
                    await _database.LocationConditionRepository.FindAsync(c => c.ConfigId == entity.ConfigId, cancellationToken);

                await ApplyUpdateToEntity(entity, model);
                _database.LocationConfigurationRepository.Update(entity);
            }

            await _database.SaveChangesAsync();

            updatedEntities.Clear();
            updatedEntities.AddRange(entities.Select(ProjectToModel));

            InvalidateConditionsCache(facilityId);
        }, cancellationToken);

        // Re-evaluate once after the full facility update commits. CancellationToken.None: the update is
        // committed, so a caller cancelling now must not skip the refresh and leave
        // IsOrgLocation/MappedToOrg stale.
        await _locationMappingService.ReevaluateLocationMappingsAsync(facilityId, CancellationToken.None);

        return updatedEntities;
    }

    public async Task DeleteByIdAsync(int configId, CancellationToken cancellationToken = default)
    {
        string? facilityId = null;

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
            facilityId = entity.FacilityId;
        });

        // Re-evaluate after the delete commits — removing the last active condition demotes the
        // facility's cached Locations to non-org (IsOrgLocationAsync returns false with no conditions).
        // CancellationToken.None: the delete is committed, so a caller cancelling now must not skip this.
        if (facilityId != null)
        {
            await _locationMappingService.ReevaluateLocationMappingsAsync(facilityId, CancellationToken.None);
        }
    }

    public async Task DeleteByFacilityIdAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        // Delete every config for the facility in a single unit of work, then invalidate and
        // re-evaluate mappings once — not once per config (ReevaluateLocationMappingsAsync reloads
        // all of the facility's Location mappings + cached bodies, so per-row re-evaluation is wasteful).
        var deleted = false;

        await _database.ExecuteInTransactionAsync(async () =>
        {
            var entities = await _database.LocationConfigurationRepository
                .FindAsync(c => c.FacilityId == facilityId, cancellationToken);

            if (entities.Count == 0)
            {
                return;
            }

            var configIds = entities.Select(e => e.ConfigId).ToList();
            var conditions = await _database.LocationConditionRepository
                .FindAsync(c => configIds.Contains(c.ConfigId), cancellationToken);

            foreach (var condition in conditions)
            {
                _database.LocationConditionRepository.Remove(condition);
            }

            foreach (var entity in entities)
            {
                _database.LocationConfigurationRepository.Remove(entity);
            }

            await _database.SaveChangesAsync();

            InvalidateConditionsCache(facilityId);
            deleted = true;
        }, cancellationToken);

        // Re-evaluate after the batch delete commits — removing the facility's active conditions
        // demotes its cached Locations to non-org (IsOrgLocationAsync returns false with no conditions).
        // CancellationToken.None: the deletes are committed, so a caller cancelling now must not skip this.
        if (deleted)
        {
            await _locationMappingService.ReevaluateLocationMappingsAsync(facilityId, CancellationToken.None);
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