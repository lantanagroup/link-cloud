using KellermanSoftware.CompareNetObjects;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Services;
using MediatR;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryPlanConfig;

public class SaveQueryPlanCommand : IRequest<QueryPlan?>
{
    public string? FacilityId { get; set; }
    public QueryPlan? QueryPlan { get; set; }
}

public class SaveQueryPlanCommandHandler : IRequestHandler<SaveQueryPlanCommand, QueryPlan?>
{
    private readonly ILogger<SaveQueryPlanCommandHandler> _logger;
    private readonly IQueryPlanRepository _repository;
    private readonly IMediator _mediator;
    private readonly CompareLogic _compareLogic;
    private readonly ITenantApiService _tenantApiService;

    public SaveQueryPlanCommandHandler(ILogger<SaveQueryPlanCommandHandler> logger, IQueryPlanRepository repository, IMediator mediator, ITenantApiService tenantApiService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _tenantApiService = tenantApiService ?? throw new ArgumentNullException(nameof(tenantApiService));
        _compareLogic = new CompareLogic();
        _compareLogic.Config.MaxDifferences = 25;        
    }

    public async Task<QueryPlan?> Handle(SaveQueryPlanCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.FacilityId))
        {
            throw new BadRequestException($"FacilityId is null or empty");
        }

        if (request.QueryPlan == null)
        {
            throw new BadRequestException($"QueryPlan is null or empty");
        }

        if (await _tenantApiService.CheckFacilityExists(request.FacilityId, cancellationToken) == false)
        {
            throw new MissingFacilityConfigurationException($"Facility {request.FacilityId} not found.");
        }

        var plan = await _repository.GetAsync(request.FacilityId, cancellationToken);

        var queryPlan = request.QueryPlan;
        if (queryPlan != null)
        {
            if (plan == null)
            {
                plan = await _repository.AddAsync(queryPlan, cancellationToken);
            }
            else
            {
                plan = await _repository.UpdateAsync(queryPlan, cancellationToken);
            }
        }

        return plan;
    }
}
