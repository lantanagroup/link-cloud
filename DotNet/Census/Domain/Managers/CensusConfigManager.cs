using Census.Domain.Entities;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models.Exceptions;
using LantanaGroup.Link.Census.Domain.Repositories;
using LantanaGroup.Link.Census.Models;
using LantanaGroup.Link.Shared.Application.Services;
using Quartz;

namespace LantanaGroup.Link.Census.Domain.Managers;

public interface ICensusConfigManager
{
    Task<CensusConfigModel> CreateAsync(CreateCensusConfigModel model, CancellationToken cancellationToken = default);
    Task<CensusConfigModel> UpdateAsync(UpdateCensusConfigModel model, CancellationToken cancellationToken = default);
    Task DeleteAsync(string facilityId, CancellationToken cancellationToken = default);
}

public class CensusConfigManager : ICensusConfigManager
{
    private readonly IDatabase _database;
    private readonly ILogger<CensusConfigManager> _logger;
    private readonly ITenantApiService _tenantApiService;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ICensusSchedulingRepository _censusSchedulingRepo;
    private readonly IPatientEventQueries _patienteventQueries;

    public CensusConfigManager(ILogger<CensusConfigManager> logger,
        IDatabase database, 
        ITenantApiService tenantApiService,
        ISchedulerFactory schedulerFactory, ICensusSchedulingRepository censusSchedulingRepo)
    {
        _database = database;
        _logger = logger;
        _tenantApiService = tenantApiService;
        _schedulerFactory = schedulerFactory;
        _censusSchedulingRepo = censusSchedulingRepo;
        _patienteventQueries = patienteventQueries;
    }

    public async Task<CensusConfigModel> CreateAsync(CreateCensusConfigModel model, CancellationToken cancellationToken = default)
    {
        if (model == null)
        {
            throw new ArgumentNullException(nameof(model));
        }

        if (await _tenantApiService.CheckFacilityExists(model.FacilityId, cancellationToken) == false)
        {
            throw new MissingTenantConfigurationException($"Facility {model.FacilityId} not found.");
        }

        var entity = new CensusConfigEntity
        {
            FacilityID = model.FacilityId,
            ScheduledTrigger = model.ScheduledTrigger,
            CreateDate = DateTime.UtcNow,
            ModifyDate = DateTime.UtcNow
        };

        try
        {
            await _database.BeginTransactionAsync(cancellationToken);

            await _database.CensusConfigRepository.AddAsync(entity, cancellationToken);

            await _censusSchedulingRepo.AddJobForFacility(CensusConfigModel.FromDomain(entity), await _schedulerFactory.GetScheduler(cancellationToken));

            await _database.SaveChangesAsync();

            await _database.CommitTransactionAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in CensusConfigManager.AddAsync");
            await _database.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        return CensusConfigModel.FromDomain(entity);
    }

    public async Task<CensusConfigModel> UpdateAsync(UpdateCensusConfigModel model, CancellationToken cancellationToken = default)
    {
        if (model == null)
        {
            throw new ArgumentNullException(nameof(model));
        }

        if (await _tenantApiService.CheckFacilityExists(model.FacilityId, cancellationToken) == false)
        {
            throw new MissingTenantConfigurationException($"Facility {model.FacilityId} not found.");
        }

        var existingEntity = await _database.CensusConfigRepository.SingleOrDefaultAsync(c => c.FacilityID == model.FacilityId, cancellationToken);

        if (existingEntity == null)
        {
            throw new KeyNotFoundException($"CensusConfig for FacilityId {model.FacilityId} not found.");
        }

        existingEntity.ScheduledTrigger = model.ScheduledTrigger;
        existingEntity.ModifyDate = DateTime.UtcNow;

        try
        {
            await _database.CensusConfigRepository.StartTransactionAsync(cancellationToken);

            _database.CensusConfigRepository.Update(existingEntity);

            await _censusSchedulingRepo.UpdateJobsForFacility(CensusConfigModel.FromDomain(existingEntity), await _schedulerFactory.GetScheduler(cancellationToken));

            await _database.SaveChangesAsync();

            await _database.CensusConfigRepository.CommitTransactionAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in CensusConfigManager.UpdateAsync");
            await _database.CensusConfigRepository.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        return CensusConfigModel.FromDomain(existingEntity);
    }

    public async Task DeleteAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var existing = await _database.CensusConfigRepository.SingleOrDefaultAsync(c => c.FacilityID == facilityId, cancellationToken);
        if (existing == null)
        {
            throw new KeyNotFoundException($"CensusConfig for FacilityId {facilityId} not found.");
        }

        _database.CensusConfigRepository.Remove(existing);
        await _database.SaveChangesAsync();
    }
}

