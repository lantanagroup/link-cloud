using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using MediatR;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryPlanConfig;

public class GetQueryPlanQuery : IRequest<IQueryPlan>
{
    public string FacilityId { get; set; }
    public QueryPlanType QueryPlanType { get; set; }
    public bool SystemPlans { get; set; }
}

public class GetQueryPlanQueryHandler : IRequestHandler<GetQueryPlanQuery, IQueryPlan>
{
    private readonly ILogger<GetQueryPlanQueryHandler> _logger;
    private readonly IQueryPlanRepository _queryPlanRepository;

    public GetQueryPlanQueryHandler(ILogger<GetQueryPlanQueryHandler> logger, IQueryPlanRepository queryPlanRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _queryPlanRepository = queryPlanRepository ?? throw new ArgumentNullException(nameof(queryPlanRepository));
    }

    public async Task<IQueryPlan> Handle(GetQueryPlanQuery request, CancellationToken cancellationToken)
    {
        var queryPlanResult = await _queryPlanRepository.GetAsync(request.FacilityId, cancellationToken);

        IQueryPlan? result = request.QueryPlanType switch
        {
            QueryPlanType.QueryPlans => new QueryPlanResult
            {
                QueryPlan = queryPlanResult,
            },
            QueryPlanType.InitialQueries => new InitialQueryResult
            {
                InitialQueries = queryPlanResult.InitialQueries
            },
            QueryPlanType.SupplementalQueries => new SupplementalQueryResult
            {
                SupplementalQueries = queryPlanResult.SupplementalQueries
            },
            _ => null
        };

        return result;
    }
}
