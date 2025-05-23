using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Factories.ParameterFactories;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ParameterQuery;
using OperationType =  LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.OperationType;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Factories.QueryFactories;

public class ParameterQueryFactory
{
    /// <summary>
    /// Operation Type will always be returned as Search.
    /// See https://lantana.atlassian.net/wiki/spaces/LSD/pages/214597642/Data+Acquisition+V2#When-a-DataAcquisitionRequested-event-is-consumed%3A:~:text=Parameter%20Query%20Config
    /// for more details.
    /// </summary>
    /// <param name="config"></param>
    /// <returns></returns>
    public static ParameterQueryFactoryResult Build(ParameterQueryConfig config, GetPatientDataRequest request, ScheduledReport scheduledReport, string lookback, List<string> resourceIds = null)
    {
        var isPaged = false;
        var searchParams = new SearchParams();
        List<SearchParams> searchParamList = new List<SearchParams>();

        foreach (var parameter in config.Parameters)
        {
            ParameterFactoryResult searchParam = parameter switch
            {
                LiteralParameter => LiteralParameterFactory.Build((LiteralParameter)parameter),
                VariableParameter => VariableParameterFactory.Build((VariableParameter)parameter, request, scheduledReport, lookback),
                ResourceIdsParameter => ResourceIdParameterFactory.Build((ResourceIdsParameter)parameter, request, resourceIds),
                _ => throw new Exception("Unable to determine parameter type."),
            };

            if(searchParam == null)
            {
                continue;
            }

            if (searchParam.paged)
            {
                isPaged = true;
                foreach(var idList in searchParam.values)
                {
                    var searchParamsCopy = searchParams;
                    searchParamsCopy.Add(searchParam.key, string.Join(",",idList));
                    searchParamList.Add(searchParamsCopy);
                }
            }
            else
            {
                searchParams.Add(searchParam.key, searchParam.value);
            }   
        }

        return isPaged ? new PagedParameterQueryFactoryResult(OperationType.Search, searchParamList)
            : new SingularParameterQueryFactoryResult(OperationType.Search, searchParams);
    }
}
