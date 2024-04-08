using Census.Domain.Entities;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Exceptions;
using LantanaGroup.Link.Shared.Application.Services;
using MediatR;
using Quartz;

namespace LantanaGroup.Link.Census.Application.Commands;

public class CreateCensusConfigCommand : IRequest
{
    public CensusConfigModel CensusConfigEntity { get; set; }
}

public class CreateCensusConfigCommandHandler : IRequestHandler<CreateCensusConfigCommand>
{
    private readonly ILogger<CreateCensusConfigCommandHandler> _logger;
    private readonly ICensusConfigRepository _censusConfigService;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ICensusSchedulingRepository _censusSchedulingRepo;
    private readonly IMediator _mediator;
    private readonly ITenantApiService _tenantApiService;

    public CreateCensusConfigCommandHandler(ILogger<CreateCensusConfigCommandHandler> logger, ICensusConfigRepository censusConfigService, ISchedulerFactory schedulerFactory, ICensusSchedulingRepository censusSchedulingRepo, IMediator mediator, ITenantApiService tenantApiService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _censusConfigService = censusConfigService ?? throw new ArgumentNullException(nameof(censusConfigService));
        _schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
        _censusSchedulingRepo = censusSchedulingRepo ?? throw new ArgumentNullException(nameof(censusSchedulingRepo));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _tenantApiService = tenantApiService ?? throw new ArgumentNullException(nameof(tenantApiService));
    }

    public async Task Handle(CreateCensusConfigCommand request, CancellationToken cancellationToken)
    {
        if (await _tenantApiService.CheckFacilityExists(request.CensusConfigEntity.FacilityId, cancellationToken) == false)
        {
            throw new MissingTenantConfigurationException($"Facility {request.CensusConfigEntity.FacilityId} not found.");
        }

        var existingEntity = await _censusConfigService.GetByFacilityIdAsync(request.CensusConfigEntity.FacilityId);

        if (existingEntity != null)
        {
            var oldEntity = existingEntity;
            existingEntity.ScheduledTrigger = request.CensusConfigEntity.ScheduledTrigger;
            existingEntity.ModifyDate = DateTime.UtcNow;
            try
            {
                await _censusSchedulingRepo.UpdateJobsForFacility(existingEntity, oldEntity, await _schedulerFactory.GetScheduler(cancellationToken));
            }
            catch (Exception ex)
            {
                var message = $"Error re-scheduling job for facility {existingEntity.FacilityID} {ex.Message}\n{ex.InnerException}\n{ex.Source}\n{ex.StackTrace}";
                _logger.LogError(message, ex);
                throw;
            }

            try
            {
                await _censusConfigService.UpdateAsync(existingEntity, cancellationToken);
            }
            catch (Exception ex)
            {
                var message = $"Error saving config for facility {existingEntity.FacilityID} {ex.Message}\n{ex.InnerException}\n{ex.Source}\n{ex.StackTrace}";
                _logger.LogError(message, ex);
                throw;
            }
        }
        else
        {
            existingEntity = new CensusConfigEntity
            {
                Id = Guid.NewGuid().ToString(),
                FacilityID = request.CensusConfigEntity.FacilityId,
                ScheduledTrigger = request.CensusConfigEntity.ScheduledTrigger,
                CreateDate = DateTime.UtcNow,
                ModifyDate = DateTime.UtcNow,
            };

            try
            {
                await _censusSchedulingRepo.AddJobForFacility(existingEntity, await _schedulerFactory.GetScheduler(cancellationToken));
            }
            catch (Exception ex)
            {
                var message = $"Error scheduling job for facility {existingEntity.FacilityID}\n{ex.Message}\n{ex.InnerException}\n{ex.Source}\n{ex.StackTrace}";
                _logger.LogError(message, ex);
                throw;
            }

            try
            {
                await _censusConfigService.AddAsync(existingEntity, cancellationToken);
            }
            catch (Exception ex)
            {
                var message = $"Error saving config for facility {existingEntity.FacilityID} {ex.Message}\n{ex.InnerException}\n{ex.Source}\n{ex.StackTrace}";
                throw;
            }
        }
    }
}
