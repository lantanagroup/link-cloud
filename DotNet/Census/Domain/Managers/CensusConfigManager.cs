﻿using Census.Domain.Entities;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Exceptions;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Application.Services;
using Microsoft.EntityFrameworkCore;
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
    private readonly IEntityRepository<CensusConfigEntity> _censusConfigRepository;
    private readonly ITenantApiService _tenantApiService;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ICensusSchedulingRepository _censusSchedulingRepo;

    public CensusConfigManager(ILogger<CensusConfigManager> logger,
        IEntityRepository<CensusConfigEntity> censusConfigRepository, ITenantApiService tenantApiService,
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
                await _censusSchedulingRepo.UpdateJobsForFacility(existingEntity,
                    await _schedulerFactory.GetScheduler(cancellationToken));
            }
            catch (Exception ex)
            {
                var message =
                    $"Error re-scheduling job for facility {existingEntity.FacilityID} {ex.Message}\n{ex.InnerException}\n{ex.Source}\n{ex.StackTrace}";
                _logger.LogError(message, ex);
                throw;
            }

            try
            {
                await _censusConfigRepository.UpdateAsync(existingEntity, cancellationToken);
            }
            catch (Exception ex)
            {
                var message =
                    $"Error saving config for facility {existingEntity.FacilityID} {ex.Message}\n{ex.InnerException}\n{ex.Source}\n{ex.StackTrace}";
                _logger.LogError(message, ex);
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
                await _censusSchedulingRepo.AddJobForFacility(existingEntity,
                    await _schedulerFactory.GetScheduler(cancellationToken));
            }
            catch (Exception ex)
            {
                var message =
                    $"Error scheduling job for facility {existingEntity.FacilityID}\n{ex.Message}\n{ex.InnerException}\n{ex.Source}\n{ex.StackTrace}";
                _logger.LogError(message, ex);
                throw;
            }

            try
            {
                await _censusConfigRepository.AddAsync(existingEntity, cancellationToken);
            }
            catch (Exception ex)
            {
                //TODO: Daniel - doesn't do anything with the message
                var message =
                    $"Error saving config for facility {existingEntity.FacilityID} {ex.Message}\n{ex.InnerException}\n{ex.Source}\n{ex.StackTrace}";
                throw;
            }
        }

        return existingEntity;
    }
}
