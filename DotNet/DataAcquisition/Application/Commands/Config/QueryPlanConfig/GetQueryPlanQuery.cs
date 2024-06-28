using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using MediatR;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryPlanConfig;

public class GetQueryPlanQuery : IRequest<QueryPlan?>
{
    public string FacilityId { get; set; }
}

public class GetQueryPlanQueryHandler : IRequestHandler<GetQueryPlanQuery, QueryPlan?>
{
    private readonly ILogger<GetQueryPlanQueryHandler> _logger;
    private readonly IEntityRepository<QueryPlan> _queryPlanRepository;

    public GetQueryPlanQueryHandler(ILogger<GetQueryPlanQueryHandler> logger, IEntityRepository<QueryPlan> queryPlanRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _queryPlanRepository = queryPlanRepository ?? throw new ArgumentNullException(nameof(queryPlanRepository));
    }

    public async Task<QueryPlan?> Handle(GetQueryPlanQuery request, CancellationToken cancellationToken)
    {
        var queryPlanResult = await _queryPlanRepository.GetAsync(request.FacilityId, cancellationToken);

        if (queryPlanResult == null) 
            return null;

        return queryPlanResult;
    }
}
