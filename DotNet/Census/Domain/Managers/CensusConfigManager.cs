using Census.Domain.Entities;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Exceptions;
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

    public CensusConfigManager(ILogger<CensusConfigManager> logger,
        IBaseEntityRepository<CensusConfigEntity> censusConfigRepository, ITenantApiService tenantApiService,
        ISchedulerFactory schedulerFactory, ICensusSchedulingRepository censusSchedulingRepo)
    {
        _logger = logger;
        _censusConfigRepository = censusConfigRepository;
        _tenantApiService = tenantApiService;
        _schedulerFactory = schedulerFactory;
        _censusSchedulingRepo = censusSchedulingRepo;
    }

    public async Task<IEnumerable<CensusConfigEntity>> GetAllFacilities(CancellationToken cancellationToken = default)
    {
        return await _censusConfigRepository.GetAllAsync(cancellationToken);
    }

    public async Task DeleteCensusConfigByFacilityId(string facilityId, CancellationToken cancellationToken = default)
    {
        var existing = await _censusConfigRepository.SingleOrDefaultAsync(c => c.FacilityID == facilityId, cancellationToken);
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

            try
            {
                await _censusConfigRepository.StartTransactionAsync(cancellationToken);

                await _censusConfigRepository.UpdateAsync(existingEntity, cancellationToken);

                await _censusSchedulingRepo.UpdateJobsForFacility(existingEntity,
                await _schedulerFactory.GetScheduler(cancellationToken));

                await _censusConfigRepository.CommitTransactionAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in CensusConfigManager.AddOrUpdateCensusConfig");
                await _censusConfigRepository.RollbackTransactionAsync(cancellationToken);
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
                CreateDate = DateTime.UtcNow,
                ModifyDate = DateTime.UtcNow,
            };

            try
            {
                await _censusConfigRepository.StartTransactionAsync(cancellationToken);

                await _censusConfigRepository.AddAsync(existingEntity, cancellationToken);

                await _censusSchedulingRepo.AddJobForFacility(existingEntity,
                await _schedulerFactory.GetScheduler(cancellationToken));

                await _censusConfigRepository.CommitTransactionAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in CensusConfigManager.AddOrUpdateCensusConfig");
                await _censusConfigRepository.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }

        return existingEntity;
    }
}
