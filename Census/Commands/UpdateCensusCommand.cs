using Census.Models;
using Census.Repositories;
using Census.Services;
using LantanaGroup.Link.Census.Application.Interfaces;
using MediatR;
using Quartz;

namespace Census.Commands;

public class UpdateCensusCommand : IRequest<CensusConfigModel>
{
    public CensusConfigModel Config;
}

public class UpdateCensusCommandHandler : IRequestHandler<UpdateCensusCommand, CensusConfigModel>
{
    private readonly ILogger<UpdateCensusCommandHandler> _logger;
    private readonly ICensusConfigMongoRepository _repository;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ICensusSchedulingRepository _censusSchedulingRepo;

    public UpdateCensusCommandHandler(ILogger<UpdateCensusCommandHandler> logger, ICensusConfigMongoRepository repository, ISchedulerFactory schedulerFactory, ICensusSchedulingRepository censusSchedulingRepo)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
        _censusSchedulingRepo = censusSchedulingRepo ?? throw new ArgumentNullException(nameof(censusSchedulingRepo));
    }

    public async Task<CensusConfigModel> Handle(UpdateCensusCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"Updating config for facility {request.Config}.");
        var entity = await _repository.GetAsync(request.Config.FacilityId, cancellationToken);
        var oldEntity = entity;
        if (entity == null) { throw new Exception($"Unable to retrieve entity for facility {request.Config.FacilityId}"); }
        entity.ScheduledTrigger = request.Config.ScheduledTrigger;
        entity = await _repository.UpdateAsync(entity, cancellationToken);
        await _censusSchedulingRepo.UpdateJobsForFacility(entity, oldEntity, await _schedulerFactory.GetScheduler(cancellationToken));
        return new CensusConfigModel
        {
            FacilityId = entity.FacilityID,
            ScheduledTrigger = entity.ScheduledTrigger
        };
    }
}
