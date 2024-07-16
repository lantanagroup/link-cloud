using DataAcquisition.Domain;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using MongoDB.Driver;

namespace LantanaGroup.Link.DataAcquisition.Application.Repositories;

public interface IQueriedFhirResourceManager
{
    Task<QueryResultsModel> GetQueryResultsAsync(string queryType, string correlationId, bool successOnly,
        CancellationToken cancellationToken = default);

    Task<QueriedFhirResourceRecord> AddAsync(QueriedFhirResourceRecord entity,  CancellationToken cancellationToken = default);
}

public class QueriedFhirResourceManager : IQueriedFhirResourceManager
{
    private readonly ILogger<QueriedFhirResourceManager> _logger;
    private readonly IDatabase _database;

    public QueriedFhirResourceManager(ILogger<QueriedFhirResourceManager> logger, IDatabase database)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<QueriedFhirResourceRecord> AddAsync(QueriedFhirResourceRecord entity, CancellationToken cancellationToken = default)
    {
        return await _database.QueriedFhirResourceRepository.AddAsync(entity, cancellationToken);
    }

    public async Task<QueryResultsModel> GetQueryResultsAsync(string queryType, string correlationId, bool successOnly, CancellationToken cancellationToken = default)
    {

        //TODO: Doing this for now to not interfere with any other dependecies of the QueryPlanType enum. The return of this is to align with Query Type values that are sent before and after DataAcquisitionRequested events
        if (string.Equals(queryType, "initial", StringComparison.InvariantCultureIgnoreCase))
        {
            queryType = QueryPlanType.Initial.ToString();
        }
        else if (string.Equals(queryType, "supplemental", StringComparison.InvariantCultureIgnoreCase))
        {
            queryType = QueryPlanType.Supplemental.ToString();
        }

        var resultSet = await _database.QueriedFhirResourceRepository
                 .FindAsync(x =>
                x.CorrelationId == correlationId
                && (!successOnly || x.IsSuccessful == true)
                && x.QueryType == queryType, cancellationToken);

        var queryResults = new QueryResultsModel() { QueryResults = new List<Models.QueryResult>() };

        if (resultSet != null && resultSet.Count > 0)
        {
            queryResults.PatientId = resultSet.Select(x => x.PatientId).FirstOrDefault();

            resultSet.ForEach(x => queryResults.QueryResults.Add(new Models.QueryResult
            {
                QueryType = x.QueryType == QueryPlanType.Initial.ToString() ? "Initial" : "Supplemental",
                ResourceId = x.ResourceId,
                ResourceType = x.ResourceType,
                IsSuccessful = x.IsSuccessful
            }));
        }

        return queryResults;
    }
}
