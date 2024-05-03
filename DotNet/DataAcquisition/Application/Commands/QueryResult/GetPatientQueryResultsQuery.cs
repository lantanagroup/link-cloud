using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using MediatR;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.QueryResult;

public class GetPatientQueryResultsQuery : IRequest<QueryResultsModel>
{
    public string CorrelationId { get; set; }
    public string QueryType { get; set; }
    public bool SuccessOnly { get; set; }
}

public class GetPatientQueryResultsQueryHandler : IRequestHandler<GetPatientQueryResultsQuery, QueryResultsModel>
{
    private readonly ILogger<GetPatientQueryResultsQueryHandler> _logger;
    private readonly IQueriedFhirResourceRepository _queriedFhirResourceRepository;

    public GetPatientQueryResultsQueryHandler(ILogger<GetPatientQueryResultsQueryHandler> logger, IQueriedFhirResourceRepository queriedFhirResourceRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _queriedFhirResourceRepository = queriedFhirResourceRepository ?? throw new ArgumentNullException(nameof(queriedFhirResourceRepository));
    }

    public async Task<QueryResultsModel> Handle(GetPatientQueryResultsQuery request, CancellationToken cancellationToken)
    {
        string queryType = request.QueryType;

        //TODO: Doing this for now to not interfere with any other dependecies of the QueryPlanType enum. The return of this is to align with Query Type values that are sent before and after DataAcquisitionRequested events
        if (string.Equals(queryType, "initial", StringComparison.InvariantCultureIgnoreCase))
        {
            queryType = QueryPlanType.InitialQueries.ToString();
        }
        else if (string.Equals(queryType, "supplemental", StringComparison.InvariantCultureIgnoreCase))
        {
            queryType = QueryPlanType.SupplementalQueries.ToString();
        }

        var resultSet = await _queriedFhirResourceRepository.GetQueryResultsAsync(request.CorrelationId, queryType, request.SuccessOnly);

        var queryResults = new QueryResultsModel() { QueryResults = new List<Models.QueryResult>() } ;

        if (resultSet != null && resultSet.Count > 0)
        {
            queryResults.PatientId = resultSet.Select(x => x.PatientId).FirstOrDefault();

            resultSet.ForEach(x => queryResults.QueryResults.Add(new Models.QueryResult
            {
                QueryType = x.QueryType == QueryPlanType.InitialQueries.ToString() ? "Initial" : "Supplemental",
                ResourceId = x.ResourceId,
                ResourceType = x.ResourceType,
                IsSuccessful = x.IsSuccessful
            }));
        }

        return queryResults;
    }
}
