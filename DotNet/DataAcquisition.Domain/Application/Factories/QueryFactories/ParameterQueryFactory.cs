using LantanaGroup.Link.DataAcquisition.Domain.Application.Factories.ParameterFactories;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ParameterQuery;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using OperationType = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.OperationType;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Factories.QueryFactories;

public class ParameterQueryFactory : IParameterQueryFactory
{
    private readonly ILogger<ParameterQueryFactory> _logger;
    private readonly ILiteralParameterFactory _literalParameterFactory;
    private readonly IVariableParameterFactory _variableParameterFactory;
    private readonly IResourceIdParameterFactory _resourceIdParameterFactory;

    public ParameterQueryFactory(ILogger<ParameterQueryFactory> logger, ILiteralParameterFactory literalParameterFactory, IVariableParameterFactory variableParameterFactory, IResourceIdParameterFactory resourceIdParameterFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _literalParameterFactory = literalParameterFactory ?? throw new ArgumentNullException(nameof(literalParameterFactory));
        _variableParameterFactory = variableParameterFactory ?? throw new ArgumentNullException(nameof(variableParameterFactory));
        _resourceIdParameterFactory = resourceIdParameterFactory ?? throw new ArgumentNullException(nameof(resourceIdParameterFactory));
    }

    public async Task<ParameterQueryFactoryResult> Build(ParameterQueryConfig config, GetPatientDataRequest request, ScheduledReport scheduledReport, string lookback, IDataAcquisitionLogQueries dataAcquisitionLogQueries)
    {
        var isPaged = false;
        var searchParams = new List<KeyValuePair<string, string>>();
        List<List<KeyValuePair<string, string>>> searchParamList = new List<List<KeyValuePair<string, string>>>();

        foreach (var parameter in config.Parameters)
        {
            ParameterFactoryResult? searchParam = parameter switch
            {
                LiteralParameter literalParameter => _literalParameterFactory.Build(literalParameter),
                VariableParameter variableParameter => _variableParameterFactory.Build(variableParameter, request, scheduledReport, lookback),
                ResourceIdsParameter idsParameter => await _resourceIdParameterFactory.Build(idsParameter, request, dataAcquisitionLogQueries),
                _ => throw new Exception("Unable to determine parameter type."),
            };

            if (searchParam == null)
                continue;

            if (searchParam.paged)
            {
                if (isPaged)
                {
                    _logger.LogError("Query plan cannot have multiple paged parameters per resource type: {ResourceType}", config.ResourceType);
                    return null;
                }

                isPaged = true;

                if (searchParam.values != null)
                {
                    foreach (var idList in searchParam.values)
                    {
                        var searchParamsCopy = new List<KeyValuePair<string, string>>(searchParams);
                        searchParamsCopy.Add(new KeyValuePair<string, string>(searchParam.key, string.Join(",", idList)));
                        searchParamList.Add(searchParamsCopy);
                    }
                }
            }
            else
            {
                if (string.IsNullOrEmpty(searchParam.value))
                {
                    _logger.LogWarning("Parameter value is null or empty. Parameter Key: {Key} for {ResourceType} on CorrelationId {CorrelationId}", searchParam.key, config.ResourceType, request.CorrelationId);
                    continue;
                }

                // If another parameter previously made this a paged query, add the search param to all of the pages of the query
                if (isPaged)
                {
                    searchParamList.ForEach(x => x.Add(new KeyValuePair<string, string>(searchParam.key, searchParam.value)));
                }
                else
                {
                    searchParams.Add(new KeyValuePair<string, string>(searchParam.key, searchParam.value));
                }
            }
        }

        // Read is not a valid operation type for a parameter query, 
        // but if it is set to that, treat it as a search to avoid errors and still return results
        OperationType operationType;
        if (config.OperationType == OperationType.Read)
        {
            _logger.LogWarning(
                "Read is not a valid operation type for ParameterQueryConfig. Treating as Search for" +
                " ResourceType {ResourceType} on CorrelationId {CorrelationId}",
                config.ResourceType,
                request.CorrelationId);
            operationType = OperationType.Search;
        }
        else
        {
            operationType = config.OperationType == OperationType.SearchPost
                ? OperationType.SearchPost
                : OperationType.Search;
        }

        return isPaged ? new PagedParameterQueryFactoryResult(operationType, searchParamList)
            : new SingularParameterQueryFactoryResult(operationType, searchParams);
    }
}
