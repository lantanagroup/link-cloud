using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using MediatR;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.QueryResult;

public class GetPatientQueryResultsQuery : IRequest<QueryResultsModel>
{
    public string FacilityId { get; set; }
    public string PatientId { get; set; }
    public string CorrelationId { get; set; }
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
        var resultSet = await _queriedFhirResourceRepository.GetQueryResultsAsync(request.FacilityId, request.PatientId, request.CorrelationId, cancellationToken);
        var queryResults = new QueryResultsModel();
        resultSet.ForEach(x => queryResults.QueryResults.Add(new Models.QueryResult
        {
            QueryType = x.QueryType,
            ResourceId = x.ResourceId,
            ResourceType = x.ResourceType
        }));
        return queryResults;
    }
}
