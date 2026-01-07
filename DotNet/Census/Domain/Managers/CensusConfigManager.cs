using AngleSharp.Dom;
using Census.Domain.Entities;
using Confluent.Kafka;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Exceptions;
using LantanaGroup.Link.Census.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Quartz;

namespace LantanaGroup.Link.Census.Domain.Managers;

public interface ICensusConfigManager
{
    Task<IEnumerable<CensusConfigEntity>> GetAllFacilities(CancellationToken cancellationToken = default);
    Task DeleteCensusConfigByFacilityId(string facilityId, CancellationToken cancellationToken = default);
    Task<CensusConfigEntity?> GetCensusConfigByFacilityId(string facilityId);
    Task<CensusConfigEntity> AddOrUpdateCensusConfig(CensusConfigModel entity, CancellationToken cancellationToken = default);
}

public class CensusConfigManager : ICensusConfigManager
{
    private readonly ILogger<CensusConfigManager> _logger;
    private readonly IBaseEntityRepository<CensusConfigEntity> _censusConfigRepository;
    private readonly ITenantApiService _tenantApiService;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ICensusSchedulingRepository _censusSchedulingRepo;
    private readonly IPatientEventQueries _patienteventQueries;

    public CensusConfigManager(ILogger<CensusConfigManager> logger,
        IBaseEntityRepository<CensusConfigEntity> censusConfigRepository, ITenantApiService tenantApiService,
        ISchedulerFactory schedulerFactory, ICensusSchedulingRepository censusSchedulingRepo, IPatientEventQueries patienteventQueries)
    {
        _logger = logger;
        _censusConfigRepository = censusConfigRepository;
        _tenantApiService = tenantApiService;
        _schedulerFactory = schedulerFactory;
        _censusSchedulingRepo = censusSchedulingRepo;
        _patienteventQueries = patienteventQueries;
    }

    public async Task<IEnumerable<CensusConfigEntity>> GetAllFacilities(CancellationToken cancellationToken = default)
    {
        return await _censusConfigRepository.GetAllAsync(cancellationToken);
    }

    public async Task DeleteCensusConfigByFacilityId(string facilityId, CancellationToken cancellationToken = default)
    {
        var existing = await _censusConfigRepository.SingleOrDefaultAsync(c => c.FacilityID == facilityId, cancellationToken);
        if (existing == null)
        {
            return;
        }
        await _censusConfigRepository.DeleteAsync(existing, cancellationToken);
    }

    public async Task<CensusConfigEntity?> GetCensusConfigByFacilityId(string facilityId)
    {
        return await _censusConfigRepository.SingleOrDefaultAsync(c => c.FacilityID == facilityId);
    }

    public async Task<CensusConfigEntity> AddOrUpdateCensusConfig(CensusConfigModel entity, CancellationToken cancellationToken = default)
    {
        if (await _tenantApiService.CheckFacilityExists(entity.FacilityId, cancellationToken) == false)
        {
            throw new MissingTenantConfigurationException($"Facility {entity.FacilityId} not found.");
        }

        var existingEntity =
            await _censusConfigRepository.SingleOrDefaultAsync(c => c.FacilityID == entity.FacilityId,
                cancellationToken);

        if (existingEntity != null)
        {
            existingEntity.ScheduledTrigger = entity.ScheduledTrigger;
            existingEntity.ModifyDate = DateTime.UtcNow;
            existingEntity.Enabled = entity.Enabled ?? true; // Default to true if not specified
            
            await using var transaction = await _patienteventQueries.StartTransaction(cancellationToken);
            
            try
            {
                await _censusConfigRepository.UpdateAsync(existingEntity, cancellationToken);

                // Only update jobs if enabled
                if (existingEntity.Enabled == true)
                {
                    await _censusSchedulingRepo.UpdateJobsForFacility(existingEntity,
                        await _schedulerFactory.GetScheduler(cancellationToken));
                }
                else
                {
                    // If disabled, remove existing jobs
                    await _censusSchedulingRepo.DeleteJobsForFacility(existingEntity.FacilityID,
                        await _schedulerFactory.GetScheduler(cancellationToken));
                }

                await _patienteventQueries.CommitTransaction(transaction, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in CensusConfigManager.AddOrUpdateCensusConfig");
                await _patienteventQueries.RollbackTransaction(transaction, cancellationToken);
                throw;
            }
        }
        else
        {
            existingEntity = new CensusConfigEntity
            {
                Id = Guid.NewGuid().ToString(),
                FacilityID = entity.FacilityId,
                ScheduledTrigger = entity.ScheduledTrigger,
                Enabled = entity.Enabled ?? true, // Default to true if not specified
                CreateDate = DateTime.UtcNow,
                ModifyDate = DateTime.UtcNow,
            };

            await using var transaction = await _patienteventQueries.StartTransaction(cancellationToken);
            try
            {
                await _censusConfigRepository.AddAsync(existingEntity, cancellationToken);

                // Only create jobs if enabled
                if (existingEntity.Enabled == true)
                {
                    await _censusSchedulingRepo.AddJobForFacility(existingEntity, 
                        await _schedulerFactory.GetScheduler(cancellationToken));
                }
                
                await _patienteventQueries.CommitTransaction(transaction, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in CensusConfigManager.AddOrUpdateCensusConfig");
                await _patienteventQueries.RollbackTransaction(transaction, cancellationToken);
                throw;
            }
        }

        return existingEntity;
    }
}
