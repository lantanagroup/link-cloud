using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Exceptions;
using LantanaGroup.Link.Shared.Application.Services;
using MediatR;
using Quartz;

namespace LantanaGroup.Link.Census.Application.Commands;

public class UpdateCensusConfigCommand : IRequest<CensusConfigModel>
{
    public CensusConfigModel Config;
}

public class UpdateCensusCommandHandler : IRequestHandler<UpdateCensusConfigCommand, CensusConfigModel>
{
    private readonly ILogger<UpdateCensusCommandHandler> _logger;
    private readonly ICensusConfigRepository _repository;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ICensusSchedulingRepository _censusSchedulingRepo;
    private readonly IMediator _mediator;
    private readonly ITenantApiService _tenantApiService;
    public UpdateCensusCommandHandler(ILogger<UpdateCensusCommandHandler> logger, ICensusConfigRepository repository, ISchedulerFactory schedulerFactory, ICensusSchedulingRepository censusSchedulingRepo, IMediator mediator, ITenantApiService tenantApiService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
        _censusSchedulingRepo = censusSchedulingRepo ?? throw new ArgumentNullException(nameof(censusSchedulingRepo));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _tenantApiService = tenantApiService ?? throw new ArgumentNullException();
    }

    public async Task<CensusConfigModel> Handle(UpdateCensusConfigCommand request, CancellationToken cancellationToken)
    {
        if (await _tenantApiService.CheckFacilityExists(request.Config.FacilityId, cancellationToken) == false)
        {
            throw new MissingTenantConfigurationException($"Facility {request.Config.FacilityId} not found.");
        }

        _logger.LogInformation($"Updating config for facility {request.Config}.");

        var entity = await _repository.GetByFacilityIdAsync(request.Config.FacilityId, cancellationToken);
        
        if (entity == null) { throw new Exception($"Unable to retrieve entity for facility {request.Config.FacilityId}"); }

        entity.ScheduledTrigger = request.Config.ScheduledTrigger;

        await _repository.UpdateAsync(entity, cancellationToken);
        await _censusSchedulingRepo.UpdateJobsForFacility(entity, await _schedulerFactory.GetScheduler(cancellationToken));

        return new CensusConfigModel
        {
            FacilityId = entity.FacilityID,
            ScheduledTrigger = entity.ScheduledTrigger
        };
    }
}
