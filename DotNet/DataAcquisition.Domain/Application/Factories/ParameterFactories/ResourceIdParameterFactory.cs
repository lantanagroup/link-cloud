using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ParameterQuery;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Factories.ParameterFactories;

public class ResourceIdParameterFactory
{
    public static ParameterFactoryResult Build(ResourceIdsParameter parameter, GetPatientDataRequest request, List<string> resourceIds)
    {
        if (resourceIds == null || !resourceIds.Any())
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(parameter.Paged))
        {
            int pageSize = int.Parse(parameter.Paged);
            var pagedEntries = resourceIds.Chunk(pageSize).ToList();

            if (!pagedEntries.Any())
                return null;

            return new ParameterFactoryResult(parameter.Name, null, true, pagedEntries);
        }

        var joinedEntries = string.Join(",", resourceIds);
        if (string.IsNullOrWhiteSpace(joinedEntries))
            return null;

        return new ParameterFactoryResult(parameter.Name, joinedEntries);
    }
}
