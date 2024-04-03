using Census.Models;
using Census.Repositories;
using Census.Services;
using LantanaGroup.Link.Census.Application.Interfaces;
using MediatR;
using Quartz;

namespace LantanaGroup.Link.Census.Application.Commands;

public class DeleteCensusConfigCommand : IRequest
{
    public string FacilityId { get; set; }
}

public class DeleteCensusConfigCommandHandler : IRequestHandler<DeleteCensusConfigCommand>
{
    private readonly ILogger<DeleteCensusConfigCommandHandler> _logger;
    private readonly ICensusConfigMongoRepository _repository;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ICensusSchedulingRepository _censusSchedulingRepo;

    public DeleteCensusConfigCommandHandler(ILogger<DeleteCensusConfigCommandHandler> logger, ICensusConfigMongoRepository repository, ISchedulerFactory schedulerFactory, ICensusSchedulingRepository censusSchedulingRepo)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
        _censusSchedulingRepo = censusSchedulingRepo ?? throw new ArgumentNullException(nameof(censusSchedulingRepo));
    }

    public async Task Handle(DeleteCensusConfigCommand request, CancellationToken cancellationToken)
    {
        await _repository.DeleteAsync(request.FacilityId, cancellationToken).ConfigureAwait(false);
        await _censusSchedulingRepo.DeleteJobsForFacility(request.FacilityId, await _schedulerFactory.GetScheduler());
    }
}
