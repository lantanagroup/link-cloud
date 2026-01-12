using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ParameterQuery;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Factories.ParameterFactories;

public class ResourceIdParameterFactory
{
    private static readonly ILogger<ResourceIdParameterFactory> _logger;

    static ResourceIdParameterFactory()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<ResourceIdParameterFactory>();
    }
    public static async Task<ParameterFactoryResult?> Build(ResourceIdsParameter parameter, GetPatientDataRequest request, IDataAcquisitionLogQueries dataAcquisitionLogQueries)
    {
        List<string> resourceIds = await
            dataAcquisitionLogQueries.GetResourceIdsForReportPatient(request.CorrelationId, request.FacilityId, parameter.Resource);
        
        if (resourceIds == null || !resourceIds.Any())
        {
            _logger.LogWarning("ResourceIdsParameter validation failed: resourceIds is null or empty. Parameter Name: {Name}", parameter.Name);
            return null;
        }
        
        Int32.TryParse(parameter.Paged, out int pageSize);

        if (!string.IsNullOrWhiteSpace(parameter.Paged) && pageSize > 0 && resourceIds.Count > pageSize)
        {
            var pagedEntries = resourceIds.Chunk(pageSize).ToList();
            return new ParameterFactoryResult(parameter.Name, null, true, pagedEntries);
        }

        var joinedEntries = string.Join(",", resourceIds);
        if (string.IsNullOrWhiteSpace(joinedEntries))
            return null;

        return new ParameterFactoryResult(parameter.Name, joinedEntries);
    }
}
